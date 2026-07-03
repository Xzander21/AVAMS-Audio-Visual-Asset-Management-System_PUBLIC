namespace AVEquipmentManager.Shared.DTOs;

/// <summary>
/// Aggregated Key Performance Indicators for the AVAMS dashboard.
/// All values are computed on the fly from the underlying entities; nothing
/// is persisted.
/// </summary>
public class KpiDto
{
    /// <summary>Total active (non-archived) equipment records.</summary>
    public int TotalAssets { get; set; }

    /// <summary>Percentage of assets currently in the Active status.</summary>
    public double AvailabilityPercent { get; set; }

    /// <summary>Percentage of assets currently being utilized (Active + Under Maintenance).</summary>
    public double UtilizationPercent { get; set; }

    /// <summary>Percentage of assets within their expected service life (not expired).</summary>
    public double LifecycleCompliancePercent { get; set; }

    /// <summary>Average days between maintenance records for the same equipment item. -1 if not computable.</summary>
    public double MeanTimeBetweenFailuresDays { get; set; }

    /// <summary>Average days from Open to Resolved on maintenance records. -1 if no resolved records.</summary>
    public double MeanTimeToRepairDays { get; set; }

    /// <summary>Count of open + in-progress maintenance records.</summary>
    public int MaintenanceBacklog { get; set; }

    /// <summary>Maintenance records resolved in the last 30 days.</summary>
    public int ResolvedLast30Days { get; set; }

    /// <summary>Assets disposed in the last 90 days.</summary>
    public int DisposedLast90Days { get; set; }

    /// <summary>Assets acquired (Deployed) in the last 90 days.</summary>
    public int AcquiredLast90Days { get; set; }

    /// <summary>Items expired or expiring within the next 6 months.</summary>
    public int AtRiskAssets { get; set; }

    /// <summary>Timestamp the KPIs were computed.</summary>
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
