using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Transaction-proof contract for Disposal state changes. Every method here
/// is guaranteed by the implementation to:
///   1. Open an explicit IDbContextTransaction.
///   2. Mutate the Disposal row (and Equipment, on terminal transition).
///   3. Append exactly one immutable LifecycleLog row per state change.
///   4. Commit, or roll back as a single atomic unit on any failure.
///
/// All methods return <see cref="Result{T}"/> so business-rule failures
/// (illegal transition, missing row, duplicate open disposal) do not throw.
/// Infrastructure failures (DB outage, concurrency conflict) are caught
/// by the implementation, rolled back, and converted to Result.Fail.
/// </summary>
public interface IDisposalLifecycleService
{
    Task<Result<Disposal>> CreateAsync(
        int equipmentId, string reason, DisposalMethod method, string? notes,
        string performedByUserId, CancellationToken ct = default);

    Task<Result<Disposal>> ApproveAsync(
        int disposalId, string performedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Mark an Approved disposal as Disposed (terminal). All inside one transaction:
    ///   • Disposal.Status → Disposed, DisposedAt stamped.
    ///   • Equipment.Status → Decommissioned, IsArchived = true.
    ///   • If <paramref name="replacementEquipmentId"/> is supplied AND that unit is
    ///     currently Reserved + not archived, the spare is promoted to Active and
    ///     inherits the disposed unit's RoomName. Disposal.ReplacementEquipmentId
    ///     is written so the swap is fully auditable.
    ///   • One LifecycleLog row per entity affected.
    /// </summary>
    Task<Result<Disposal>> DisposeAsync(
        int disposalId, string performedByUserId,
        int? replacementEquipmentId = null,
        CancellationToken ct = default);

    Task<Result<Disposal>> CancelAsync(
        int disposalId, string performedByUserId, CancellationToken ct = default);
}
