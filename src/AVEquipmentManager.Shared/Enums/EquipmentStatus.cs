namespace AVEquipmentManager.Shared.Enums;

public enum EquipmentStatus
{
    /// <summary>Currently installed and in service in a classroom.</summary>
    Active            = 0,

    /// <summary>Removed from the room temporarily for repair.</summary>
    UnderMaintenance  = 1,

    /// <summary>No longer in service but still owned (pre-disposal state).</summary>
    Retired           = 2,

    /// <summary>Terminal: physically disposed by the Supervisor.</summary>
    Decommissioned    = 3,

    /// <summary>
    /// Spare unit held in reserve, not deployed to any room. May be promoted to
    /// Active (and assigned to a room) when an in-service unit is disposed and
    /// a replacement is requested via DisposalLifecycleService.DisposeAsync.
    /// </summary>
    Reserved          = 4
}
