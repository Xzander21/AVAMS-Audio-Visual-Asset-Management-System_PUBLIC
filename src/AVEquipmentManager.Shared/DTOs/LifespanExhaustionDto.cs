using AVEquipmentManager.Shared.Enums;

namespace AVEquipmentManager.Shared.DTOs;

/// <summary>
/// Per-asset lifespan-exhaustion row. Complements the aggregate
/// LifecycleCompliancePercent / AtRiskAssets KPIs in KpiDto by giving
/// the dashboard the actual list of units approaching end-of-life so
/// the disposal flow can be triggered.
/// </summary>
public record LifespanExhaustionRowDto(
    int             EquipmentId,
    string          SerialNumber,
    string          Name,
    AssetCategory   Category,
    string          RoomName,
    EquipmentStatus Status,
    DateTime        DateInstalled,
    int             ExpectedLifeInYears,
    DateTime        ExpectedEndOfLifeDate,
    double          ExhaustionPercent,
    int             DaysRemaining,
    bool            IsApproachingEndOfLife,
    bool            IsPastEndOfLife);

/// <summary>Aggregate envelope for the lifespan-exhaustion report.</summary>
public record LifespanExhaustionDto(
    int                                  ConsideredAssets,
    double                               ThresholdPercent,
    int                                  AssetsApproachingEndOfLife,
    int                                  AssetsPastEndOfLife,
    double                               AverageExhaustionPercent,
    IReadOnlyList<LifespanExhaustionRowDto> Rows);
