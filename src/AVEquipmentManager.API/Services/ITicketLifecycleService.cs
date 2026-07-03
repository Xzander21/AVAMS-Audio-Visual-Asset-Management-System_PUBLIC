using AVEquipmentManager.API.Services.Common;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Transaction-proof ticket workflow. Submitting a ticket and resolving it
/// are the two write paths that touch both the Ticket row AND (optionally)
/// the linked Equipment.Status row — exactly the V6 drift scenario from
/// the audit. Both operations run inside one IDbContextTransaction.
///
/// Submit (any role permitted by controller):
///   - Ticket row created (Status = Open)
///   - Equipment.Status auto-flips to UnderMaintenance if currently Active
///   - LifecycleLog rows for both
///
/// Resolve (Admin / Supervisor / AVStaff):
///   - Ticket row mutated (Status = Resolved, Resolution + ResolvedAt set)
///   - Equipment.Status flips back to Active if it was UnderMaintenance
///     AND no other open ticket references the same equipment
///   - LifecycleLog rows for both
/// </summary>
public interface ITicketLifecycleService
{
    Task<Result<Ticket>> SubmitAsync(
        CreateTicketDto dto, string performedByUserId, string? performerRole,
        CancellationToken ct = default);

    Task<Result<Ticket>> AcknowledgeAsync(
        int ticketId, string assignedTo, string performedByUserId,
        CancellationToken ct = default);

    Task<Result<Ticket>> ResolveAsync(
        int ticketId, string resolution, string performedByUserId,
        CancellationToken ct = default);

    Task<Result<Ticket>> CloseAsync(
        int ticketId, string performedByUserId, CancellationToken ct = default);
}
