using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Models;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Transaction-proof contract for the V1 fix:
/// AcquisitionsController.Deploy used two separate SaveChangesAsync calls
/// — a crash between them left an orphaned Equipment row. This service
/// wraps the Equipment insert + Acquisition update + LifecycleLog write
/// in one explicit IDbContextTransaction so the orphan-row failure mode
/// is structurally impossible.
/// </summary>
public interface IAcquisitionLifecycleService
{
    Task<Result<Acquisition>> DeployAsync(
        int acquisitionId, DeployAcquisitionDto dto,
        string performedByUserId, CancellationToken ct = default);
}
