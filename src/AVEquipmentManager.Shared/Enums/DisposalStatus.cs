namespace AVEquipmentManager.Shared.Enums;

/// <summary>
/// Lifecycle states of a Disposal transaction.
/// Pending  → the Admin marked the equipment for disposal
/// Approved → disposal has been authorized but not yet carried out
/// Disposed → the equipment has been physically removed via the recorded method
/// Cancelled → the disposal was reversed (equipment returns to service)
/// </summary>
public enum DisposalStatus
{
    Pending,
    Approved,
    Disposed,
    Cancelled
}
