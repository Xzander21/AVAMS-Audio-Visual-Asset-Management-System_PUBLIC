using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Transaction-proof Ticket lifecycle. Every state change wraps the
/// multi-row writes (Ticket + linked Equipment.Status + LifecycleLog
/// rows) in one explicit IDbContextTransaction with try/catch/rollback.
/// </summary>
public sealed class TicketLifecycleService : ITicketLifecycleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TicketLifecycleService> _logger;

    public TicketLifecycleService(AppDbContext db, ILogger<TicketLifecycleService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // =====================================================================
    // SUBMIT  (→ Open)
    // =====================================================================
    public async Task<Result<Ticket>> SubmitAsync(
        CreateTicketDto dto, string performedByUserId, string? performerRole,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Ticket>.Fail("Ticket title is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == dto.EquipmentId, ct);
            if (equipment is null)
                return Result<Ticket>.Fail($"Equipment with id {dto.EquipmentId} not found.");
            if (equipment.IsArchived)
                return Result<Ticket>.Fail("Cannot raise a ticket against an archived equipment record.");

            var nowUtc = DateTime.UtcNow;

            // ITSpecialists can only submit against their own name and may
            // never assign work. The controller already strips these but
            // we re-enforce here so the service is safe in isolation.
            var reportedBy = performerRole == Roles.ITSpecialist
                ? performedByUserId
                : (dto.ReportedBy ?? performedByUserId);

            var assignedTo = performerRole == Roles.ITSpecialist ? null : dto.AssignedTo;

            // 1) Ticket row.
            var ticket = new Ticket
            {
                Title            = dto.Title,
                Description      = dto.Description,
                EquipmentId      = dto.EquipmentId,
                Type             = dto.Type,
                Priority         = dto.Priority,
                Status           = TicketStatus.Open,
                ReportedBy       = reportedBy,
                AssignedTo       = assignedTo,
                ExternalTicketId = dto.ExternalTicketId,
                CreatedAt        = nowUtc
            };
            _db.Tickets.Add(ticket);

            // 2) Equipment.Status auto-flip (V6 fix): Active → UnderMaintenance.
            //    Skip if equipment is already in a non-Active state — we don't
            //    want to silently override Retired/Decommissioned.
            var fromEquipStatus = equipment.Status;
            if (equipment.Status == EquipmentStatus.Active)
            {
                equipment.Status    = EquipmentStatus.UnderMaintenance;
                equipment.UpdatedAt = nowUtc;
            }

            // First flush so the Ticket.Id exists for the LifecycleLog row.
            await _db.SaveChangesAsync(ct);

            // 3) Audit rows.
            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Ticket),
                EntityId          = ticket.Id,
                FromStatus        = "(new)",
                ToStatus          = nameof(TicketStatus.Open),
                PerformedByUserId = performedByUserId,
                Reason            = $"Ticket opened against equipment {equipment.Id} ({equipment.SerialNumber}): {dto.Title}",
                TransitionedAtUtc = nowUtc
            });

            if (fromEquipStatus == EquipmentStatus.Active &&
                equipment.Status == EquipmentStatus.UnderMaintenance)
            {
                _db.LifecycleLogs.Add(new LifecycleLog
                {
                    EntityType        = nameof(Equipment),
                    EntityId          = equipment.Id,
                    FromStatus        = nameof(EquipmentStatus.Active),
                    ToStatus          = nameof(EquipmentStatus.UnderMaintenance),
                    PerformedByUserId = performedByUserId,
                    Reason            = $"Auto-flipped to UnderMaintenance on open of Ticket {ticket.Id}.",
                    TransitionedAtUtc = nowUtc
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _db.Entry(ticket).Reference(t => t.Equipment).LoadAsync(ct);

            _logger.LogInformation(
                "Ticket {TicketId} submitted by {User} against equipment {EquipmentId}",
                ticket.Id, performedByUserId, equipment.Id);

            return Result<Ticket>.Ok(ticket);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict submitting ticket against equipment {EquipmentId}", dto.EquipmentId);
            return Result<Ticket>.Fail("Another user updated this equipment. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Ticket submit failed for equipment {EquipmentId}", dto.EquipmentId);
            return Result<Ticket>.Fail("Ticket submission failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // ACKNOWLEDGE  (Open → InProgress)
    // =====================================================================
    public async Task<Result<Ticket>> AcknowledgeAsync(
        int ticketId, string assignedTo, string performedByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assignedTo))
            return Result<Ticket>.Fail("AssignedTo is required to acknowledge a ticket.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t is null)
                return Result<Ticket>.Fail($"Ticket {ticketId} not found.");
            if (t.Status != TicketStatus.Open)
                return Result<Ticket>.Fail($"Only Open tickets can be acknowledged (current: {t.Status}).");

            var nowUtc = DateTime.UtcNow;
            var from   = t.Status.ToString();

            t.Status     = TicketStatus.InProgress;
            t.AssignedTo = assignedTo;
            t.UpdatedAt  = nowUtc;

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Ticket),
                EntityId          = t.Id,
                FromStatus        = from,
                ToStatus          = nameof(TicketStatus.InProgress),
                PerformedByUserId = performedByUserId,
                Reason            = $"Ticket acknowledged and assigned to {assignedTo}.",
                TransitionedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Ticket>.Ok(t);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict acknowledging ticket {TicketId}", ticketId);
            return Result<Ticket>.Fail("Another user updated this ticket. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Ticket acknowledge failed for {TicketId}", ticketId);
            return Result<Ticket>.Fail("Acknowledge failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // RESOLVE  (Open/InProgress → Resolved)  with Equipment.Status restore
    // =====================================================================
    public async Task<Result<Ticket>> ResolveAsync(
        int ticketId, string resolution, string performedByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resolution))
            return Result<Ticket>.Fail("A resolution note is required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var t = await _db.Tickets
                .Include(x => x.Equipment)
                .FirstOrDefaultAsync(x => x.Id == ticketId, ct);

            if (t is null)
                return Result<Ticket>.Fail($"Ticket {ticketId} not found.");
            if (t.Status != TicketStatus.Open && t.Status != TicketStatus.InProgress)
                return Result<Ticket>.Fail($"Only Open or InProgress tickets can be resolved (current: {t.Status}).");

            var nowUtc = DateTime.UtcNow;
            var from   = t.Status.ToString();

            t.Status     = TicketStatus.Resolved;
            t.Resolution = resolution;
            t.ResolvedAt = nowUtc;
            t.UpdatedAt  = nowUtc;

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Ticket),
                EntityId          = t.Id,
                FromStatus        = from,
                ToStatus          = nameof(TicketStatus.Resolved),
                PerformedByUserId = performedByUserId,
                Reason            = $"Resolved: {resolution}",
                TransitionedAtUtc = nowUtc
            });

            // V6 fix part 2: restore Equipment.Status to Active when the LAST
            // open ticket against it resolves. We check for any *other* open
            // tickets against the same equipment before flipping the status.
            if (t.Equipment is not null && t.Equipment.Status == EquipmentStatus.UnderMaintenance)
            {
                var otherOpen = await _db.Tickets.AnyAsync(x =>
                    x.EquipmentId == t.EquipmentId &&
                    x.Id != t.Id &&
                    (x.Status == TicketStatus.Open || x.Status == TicketStatus.InProgress), ct);

                if (!otherOpen)
                {
                    var fromEquip = t.Equipment.Status;
                    t.Equipment.Status    = EquipmentStatus.Active;
                    t.Equipment.UpdatedAt = nowUtc;

                    _db.LifecycleLogs.Add(new LifecycleLog
                    {
                        EntityType        = nameof(Equipment),
                        EntityId          = t.Equipment.Id,
                        FromStatus        = fromEquip.ToString(),
                        ToStatus          = nameof(EquipmentStatus.Active),
                        PerformedByUserId = performedByUserId,
                        Reason            = $"Auto-restored to Active on resolution of last open Ticket {t.Id}.",
                        TransitionedAtUtc = nowUtc
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Ticket>.Ok(t);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict resolving ticket {TicketId}", ticketId);
            return Result<Ticket>.Fail("Another user updated this ticket. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Ticket resolve failed for {TicketId}", ticketId);
            return Result<Ticket>.Fail("Resolve failed. The transaction was rolled back.");
        }
    }

    // =====================================================================
    // CLOSE  (Resolved → Closed, terminal)
    // =====================================================================
    public async Task<Result<Ticket>> CloseAsync(
        int ticketId, string performedByUserId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, ct);
            if (t is null)
                return Result<Ticket>.Fail($"Ticket {ticketId} not found.");
            if (t.Status != TicketStatus.Resolved)
                return Result<Ticket>.Fail($"Only Resolved tickets can be closed (current: {t.Status}).");

            var nowUtc = DateTime.UtcNow;
            var from   = t.Status.ToString();

            t.Status    = TicketStatus.Closed;
            t.UpdatedAt = nowUtc;

            _db.LifecycleLogs.Add(new LifecycleLog
            {
                EntityType        = nameof(Ticket),
                EntityId          = t.Id,
                FromStatus        = from,
                ToStatus          = nameof(TicketStatus.Closed),
                PerformedByUserId = performedByUserId,
                Reason            = "Ticket closed.",
                TransitionedAtUtc = nowUtc
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Ticket>.Ok(t);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict closing ticket {TicketId}", ticketId);
            return Result<Ticket>.Fail("Another user updated this ticket. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Ticket close failed for {TicketId}", ticketId);
            return Result<Ticket>.Fail("Close failed. The transaction was rolled back.");
        }
    }
}
