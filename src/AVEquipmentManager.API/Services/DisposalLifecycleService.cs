using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Concrete transaction-proof Disposal engine. Every public method opens
/// an explicit EF Core transaction, performs all writes, appends an
/// immutable LifecycleLog row, and commits — or rolls back atomically
/// on any failure.
///
/// Registered as:
///   builder.Services.AddScoped&lt;IDisposalLifecycleService, DisposalLifecycleService&gt;();
/// </summary>
public sealed class DisposalLifecycleService : IDisposalLifecycleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DisposalLifecycleService> _logger;

    public DisposalLifecycleService(AppDbContext db, ILogger<DisposalLifecycleService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // =====================================================================
    // CREATE  (Pending)
    // =====================================================================
    public async Task<Result<Disposal>> CreateAsync(
        int equipmentId, string reason, DisposalMethod method, string? notes,
        string performedByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result<Disposal>.Fail("A disposal reason is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var equipment = await _db.Equipment.FindAsync(new object?[] { equipmentId }, ct);
            if (equipment is null)
                return Result<Disposal>.Fail("Equipment does not exist.");
            if (equipment.IsArchived)
                return Result<Disposal>.Fail("Cannot dispose of an archived equipment record.");

            // Application-layer pre-check (friendly UX). The actual safety
            // net is the unique filtered index in AppDbContext that catches
            // any TOCTOU race.
            var hasOpen = await _db.Disposals.AnyAsync(d =>
                d.EquipmentId == equipmentId &&
                (d.Status == DisposalStatus.Pending || d.Status == DisposalStatus.Approved), ct);
            if (hasOpen)
                return Result<Disposal>.Fail("This equipment already has an open disposal transaction.");

            var nowUtc = DateTime.UtcNow;

            var d = new Disposal
            {
                EquipmentId   = equipmentId,
                Reason        = reason,
                Method        = method,
                Status        = DisposalStatus.Pending,
                RequestedBy   = performedByUserId,
                DisposalNotes = notes,
                CreatedAt     = nowUtc
            };
            _db.Disposals.Add(d);

            // First flush so we have a real Disposal.Id for the LifecycleLog row.
            // Both writes share the same transaction (tx), so this is still atomic.
            await _db.SaveChangesAsync(ct);

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Disposal),
                EntityId          = d.Id,
                FromStatus        = "(new)",
                ToStatus          = nameof(DisposalStatus.Pending),
                PerformedByUserId = performedByUserId,
                Reason            = $"Disposal opened for equipment {equipmentId}: {reason}",
                TransitionedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _db.Entry(d).Reference(x => x.Equipment).LoadAsync(ct);

            _logger.LogInformation(
                "Disposal {DisposalId} opened for equipment {EquipmentId} by {User}",
                d.Id, equipmentId, performedByUserId);

            return Result<Disposal>.Ok(d);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Unique-index race on Disposal open for equipment {EquipmentId}", equipmentId);
            return Result<Disposal>.Fail("This equipment already has an open disposal transaction.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Disposal creation failed for equipment {EquipmentId}", equipmentId);
            return Result<Disposal>.Fail("Disposal creation failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // APPROVE  (Pending → Approved)
    // =====================================================================
    public async Task<Result<Disposal>> ApproveAsync(
        int disposalId, string performedByUserId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var d = await _db.Disposals
                .Include(x => x.Equipment)
                .FirstOrDefaultAsync(x => x.Id == disposalId, ct);

            if (d is null)
                return Result<Disposal>.Fail($"Disposal {disposalId} not found.");
            if (d.Status != DisposalStatus.Pending)
                return Result<Disposal>.Fail(
                    $"Only Pending disposals can be approved (current: {d.Status}).");

            var nowUtc = DateTime.UtcNow;
            var from   = d.Status.ToString();

            d.Status     = DisposalStatus.Approved;
            d.ApprovedBy = performedByUserId;
            d.ApprovedAt = nowUtc;
            d.UpdatedAt  = nowUtc;

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Disposal),
                EntityId          = d.Id,
                FromStatus        = from,
                ToStatus          = nameof(DisposalStatus.Approved),
                PerformedByUserId = performedByUserId,
                Reason            = "Disposal approved.",
                TransitionedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<Disposal>.Ok(d);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict approving disposal {DisposalId}", disposalId);
            return Result<Disposal>.Fail(
                "Another user updated this disposal. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "ApproveAsync failed for disposal {DisposalId}", disposalId);
            return Result<Disposal>.Fail("Approve failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // DISPOSE  (Approved → Disposed, terminal) — multi-row write
    //
    // 2026-06-16 update: optional spare-equipment replacement. When the
    // Supervisor supplies replacementEquipmentId, this method also promotes
    // the spare (Status = Reserved → Active) and assigns it to the disposed
    // unit's room, all inside the same transaction.
    // =====================================================================
    public async Task<Result<Disposal>> DisposeAsync(
        int disposalId, string performedByUserId,
        int? replacementEquipmentId = null,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var d = await _db.Disposals
                .Include(x => x.Equipment)
                .FirstOrDefaultAsync(x => x.Id == disposalId, ct);

            if (d is null)
                return Result<Disposal>.Fail($"Disposal {disposalId} not found.");
            if (d.Status != DisposalStatus.Approved)
                return Result<Disposal>.Fail(
                    $"Only Approved disposals can be marked Disposed (current: {d.Status}).");

            // Validate the optional replacement BEFORE mutating anything so
            // we can fail fast with a clean message and no rollback churn.
            Equipment? replacement = null;
            if (replacementEquipmentId.HasValue)
            {
                replacement = await _db.Equipment.FirstOrDefaultAsync(
                    e => e.Id == replacementEquipmentId.Value, ct);

                if (replacement is null)
                    return Result<Disposal>.Fail(
                        $"Replacement equipment {replacementEquipmentId} not found.");
                if (replacement.IsArchived)
                    return Result<Disposal>.Fail(
                        $"Replacement equipment '{replacement.Name}' is archived.");
                if (replacement.Status != EquipmentStatus.Reserved)
                    return Result<Disposal>.Fail(
                        $"Replacement equipment must be in Reserved status (current: {replacement.Status}).");
                if (replacement.Id == d.EquipmentId)
                    return Result<Disposal>.Fail(
                        "An equipment cannot replace itself.");
            }

            var nowUtc       = DateTime.UtcNow;
            var fromDisposal = d.Status.ToString();
            var fromEquip    = d.Equipment?.Status.ToString();
            var disposedRoom = d.Equipment?.RoomName;

            // 1) Disposal row.
            d.Status                  = DisposalStatus.Disposed;
            d.DisposedAt              = nowUtc;
            d.UpdatedAt               = nowUtc;
            d.ReplacementEquipmentId  = replacement?.Id;   // null if no spare used

            // 2) Linked Equipment row → decommission and archive.
            if (d.Equipment is not null)
            {
                d.Equipment.Status     = EquipmentStatus.Decommissioned;
                d.Equipment.IsArchived = true;
                d.Equipment.ArchivedAt = nowUtc;
                d.Equipment.UpdatedAt  = nowUtc;
            }

            // 3) Replacement Equipment row → promote Reserved → Active, inherit room.
            if (replacement is not null)
            {
                replacement.Status    = EquipmentStatus.Active;
                replacement.RoomName  = disposedRoom ?? replacement.RoomName;
                replacement.UpdatedAt = nowUtc;
            }

            // 4) Audit rows — one per entity touched.
            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Disposal),
                EntityId          = d.Id,
                FromStatus        = fromDisposal,
                ToStatus          = nameof(DisposalStatus.Disposed),
                PerformedByUserId = performedByUserId,
                Reason            = replacement is null
                    ? $"Disposal {d.Id} reached terminal state via /dispose (no replacement)."
                    : $"Disposal {d.Id} reached terminal state via /dispose; replaced by Equipment {replacement.Id} ({replacement.SerialNumber}).",
                TransitionedAtUtc = nowUtc
            });

            if (d.Equipment is not null && fromEquip is not null)
            {
                _db.LifecycleLogs.Add(new LifecycleLog
                {
                    EntityType        = nameof(Equipment),
                    EntityId          = d.Equipment.Id,
                    FromStatus        = fromEquip,
                    ToStatus          = nameof(EquipmentStatus.Decommissioned),
                    PerformedByUserId = performedByUserId,
                    Reason            = $"Decommissioned as side-effect of Disposal {d.Id}.",
                    TransitionedAtUtc = nowUtc
                });
            }

            if (replacement is not null)
            {
                _db.LifecycleLogs.Add(new LifecycleLog
                {
                    EntityType        = nameof(Equipment),
                    EntityId          = replacement.Id,
                    FromStatus        = nameof(EquipmentStatus.Reserved),
                    ToStatus          = nameof(EquipmentStatus.Active),
                    PerformedByUserId = performedByUserId,
                    Reason            = $"Promoted from Reserved to Active to replace Equipment {d.EquipmentId} via Disposal {d.Id}; assigned to '{disposedRoom ?? "(no room copied)"}'.",
                    TransitionedAtUtc = nowUtc
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Disposed asset via disposal {DisposalId} by {User}; replacement = {Replacement}",
                d.Id, performedByUserId, replacement?.Id.ToString() ?? "(none)");

            return Result<Disposal>.Ok(d);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict disposing {DisposalId}", disposalId);
            return Result<Disposal>.Fail(
                "Another user updated this disposal. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "DisposeAsync failed for disposal {DisposalId}", disposalId);
            return Result<Disposal>.Fail("Dispose failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // CANCEL  (Pending/Approved → Cancelled)
    // =====================================================================
    public async Task<Result<Disposal>> CancelAsync(
        int disposalId, string performedByUserId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var d = await _db.Disposals
                .Include(x => x.Equipment)
                .FirstOrDefaultAsync(x => x.Id == disposalId, ct);

            if (d is null)
                return Result<Disposal>.Fail($"Disposal {disposalId} not found.");
            if (d.Status == DisposalStatus.Disposed)
                return Result<Disposal>.Fail("Cannot cancel an already-disposed transaction.");
            if (d.Status == DisposalStatus.Cancelled)
                return Result<Disposal>.Fail("Disposal is already cancelled.");

            var nowUtc = DateTime.UtcNow;
            var from   = d.Status.ToString();

            d.Status    = DisposalStatus.Cancelled;
            d.UpdatedAt = nowUtc;

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Disposal),
                EntityId          = d.Id,
                FromStatus        = from,
                ToStatus          = nameof(DisposalStatus.Cancelled),
                PerformedByUserId = performedByUserId,
                Reason            = "Disposal cancelled.",
                TransitionedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<Disposal>.Ok(d);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict cancelling disposal {DisposalId}", disposalId);
            return Result<Disposal>.Fail(
                "Another user updated this disposal. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "CancelAsync failed for disposal {DisposalId}", disposalId);
            return Result<Disposal>.Fail("Cancel failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? string.Empty;
        return msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
