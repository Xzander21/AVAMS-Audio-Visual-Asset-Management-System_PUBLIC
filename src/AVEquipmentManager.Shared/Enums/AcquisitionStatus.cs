namespace AVEquipmentManager.Shared.Enums;

/// <summary>
/// Lifecycle states of an Acquisition transaction (the Plan → Acquire → Deploy
/// phase of the ITIL asset lifecycle).
///
/// Planned   → identified as a need (planned purchase)
/// Ordered   → purchase order issued, awaiting delivery
/// Received  → physically received by the institution
/// Deployed  → installed in a room, linked to an Equipment record, in active service
/// Cancelled → the acquisition was abandoned at any point before Deployed
/// </summary>
public enum AcquisitionStatus
{
    Planned,
    Ordered,
    Received,
    Deployed,
    Cancelled
}
