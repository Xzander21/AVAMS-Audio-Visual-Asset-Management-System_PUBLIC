using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// V1 fix: collapses the original Deploy's two SaveChangesAsync calls
/// into a single explicit transaction. Equipment insert + Acquisition
/// state change + LifecycleLog rows commit atomically, or roll back as
/// one unit on any failure.
/// </summary>
public sealed class AcquisitionLifecycleService : IAcquisitionLifecycleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AcquisitionLifecycleService> _logger;

    public AcquisitionLifecycleService(AppDbContext db, ILogger<AcquisitionLifecycleService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Result<Acquisition>> DeployAsync(
        int acquisitionId, DeployAcquisitionDto dto,
        string performedByUserId, CancellationToken ct = default)
    {
        if (dto is null)
            return Result<Acquisition>.Fail("Deploy payload is required.");
        if (string.IsNullOrWhiteSpace(dto.SerialNumber))
            return Result<Acquisition>.Fail("Serial number is required to deploy.");
        if (string.IsNullOrWhiteSpace(dto.RoomName))
            return Result<Acquisition>.Fail("Room is required to deploy.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var a = await _db.Acquisitions.FirstOrDefaultAsync(x => x.Id == acquisitionId, ct);
            if (a is null)
                return Result<Acquisition>.Fail($"Acquisition {acquisitionId} not found.");
            if (a.Status != AcquisitionStatus.Received)
                return Result<Acquisition>.Fail(
                    $"Only Received acquisitions can be deployed (current: {a.Status}).");

            // Friendly UX pre-check. The real guard is the unique index on
            // Equipment.SerialNumber + the IsUniqueConstraintViolation catch below.
            if (await _db.Equipment.AnyAsync(e => e.SerialNumber == dto.SerialNumber, ct))
                return Result<Acquisition>.Fail(
                    $"Serial number '{dto.SerialNumber}' is already in use.");

            var nowUtc = DateTime.UtcNow;

            // ---- 1. Build the new Equipment row ----
            var equipment = new Equipment
            {
                Name                = a.ItemName,
                Category            = a.Category,
                SerialNumber        = dto.SerialNumber,
                RoomName            = dto.RoomName,
                DateInstalled       = dto.DateInstalled.ToUniversalTime(),
                ExpectedLifeInYears = dto.ExpectedLifeInYears,
                Status              = EquipmentStatus.Active,
                CreatedAt           = nowUtc
            };
            _db.Equipment.Add(equipment);

            // First flush inside the transaction so we have equipment.Id for the FK.
            await _db.SaveChangesAsync(ct);

            // ---- 2. Update the Acquisition row to point at the new Equipment ----
            a.Status              = AcquisitionStatus.Deployed;
            a.DeployedAt          = nowUtc;
            a.DeployedEquipmentId = equipment.Id;
            a.UpdatedAt           = nowUtc;

            // ---- 3. Two LifecycleLog rows: one for the Equipment birth, one for the Acquisition state move ----
            _db.LifecycleLogs.AddRange(
                new LifecycleLog
                {
                    EntityType        = nameof(Acquisition),
                    EntityId          = a.Id,
                    FromStatus        = nameof(AcquisitionStatus.Received),
                    ToStatus          = nameof(AcquisitionStatus.Deployed),
                    PerformedByUserId = performedByUserId,
                    Reason            = $"Deployed as equipment '{equipment.SerialNumber}'.",
                    TransitionedAtUtc = nowUtc
                },
                new LifecycleLog
                {
                    EntityType        = nameof(Equipment),
                    EntityId          = equipment.Id,
                    FromStatus        = "(new)",
                    ToStatus          = nameof(EquipmentStatus.Active),
                    PerformedByUserId = performedByUserId,
                    Reason            = $"Created via Acquisition {a.Id} deploy.",
                    TransitionedAtUtc = nowUtc
                });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Deployed acquisition {AcquisitionId} as equipment {EquipmentId} by {User}",
                a.Id, equipment.Id, performedByUserId);

            return Result<Acquisition>.Ok(a);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Serial collision deploying acquisition {AcquisitionId}", acquisitionId);
            return Result<Acquisition>.Fail($"Serial number '{dto.SerialNumber}' is already in use.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Concurrency conflict deploying acquisition {AcquisitionId}", acquisitionId);
            return Result<Acquisition>.Fail(
                "Another user updated this acquisition. Refresh and try again.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "DeployAsync failed for acquisition {AcquisitionId}", acquisitionId);
            return Result<Acquisition>.Fail("Deploy failed. The transaction was rolled back.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? string.Empty;
        return msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
