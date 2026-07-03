using AVEquipmentManager.API.Data;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Optimised LINQ-to-SQLite analytics. The exhaustion query pushes the
/// cheap filters (IsArchived, Status != Decommissioned/Retired) into SQL
/// and projects only the columns needed for the calculation. Date
/// arithmetic happens in memory because SQLite's date functions do not
/// translate cleanly through EF Core LINQ.
///
/// Divide-by-zero is suppressed at three levels: the SQL filter excludes
/// ExpectedLifeInYears &lt;= 0, the per-row math guards before dividing,
/// and the aggregate Average() is bypassed when the result list is empty.
/// </summary>
public sealed class ItilAnalyticsService : IItilAnalyticsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItilAnalyticsService> _logger;

    public ItilAnalyticsService(AppDbContext db, ILogger<ItilAnalyticsService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<LifespanExhaustionDto> GetLifespanExhaustionAsync(
        double thresholdPercent = 90.0, CancellationToken ct = default)
    {
        if (thresholdPercent < 0.0)   thresholdPercent = 0.0;
        if (thresholdPercent > 100.0) thresholdPercent = 100.0;

        var nowUtc = DateTime.UtcNow;

        var raw = await _db.Equipment
            .AsNoTracking()
            .Where(e => !e.IsArchived)
            .Where(e => e.Status != EquipmentStatus.Decommissioned)
            .Where(e => e.ExpectedLifeInYears > 0)
            .Select(e => new
            {
                e.Id,
                e.SerialNumber,
                e.Name,
                e.Category,
                e.RoomName,
                e.Status,
                e.DateInstalled,
                e.ExpectedLifeInYears
            })
            .ToListAsync(ct);

        if (raw.Count == 0)
        {
            return new LifespanExhaustionDto(
                ConsideredAssets:           0,
                ThresholdPercent:           thresholdPercent,
                AssetsApproachingEndOfLife: 0,
                AssetsPastEndOfLife:        0,
                AverageExhaustionPercent:   0.0,
                Rows:                       Array.Empty<LifespanExhaustionRowDto>());
        }

        var rows = raw
            .Select(e =>
            {
                double expectedDays = e.ExpectedLifeInYears * 365.25;
                double elapsedDays  = (nowUtc - e.DateInstalled).TotalDays;

                double percent = expectedDays <= 0.0
                    ? 0.0
                    : Math.Round(elapsedDays / expectedDays * 100.0, 2);

                var endOfLife    = e.DateInstalled.AddYears(e.ExpectedLifeInYears);
                int daysRemaining = (int)Math.Round((endOfLife - nowUtc).TotalDays);

                bool past        = percent >= 100.0;
                bool approaching = !past && percent >= thresholdPercent;

                return new LifespanExhaustionRowDto(
                    EquipmentId:           e.Id,
                    SerialNumber:          e.SerialNumber,
                    Name:                  e.Name,
                    Category:              e.Category,
                    RoomName:              e.RoomName,
                    Status:                e.Status,
                    DateInstalled:         e.DateInstalled,
                    ExpectedLifeInYears:   e.ExpectedLifeInYears,
                    ExpectedEndOfLifeDate: endOfLife,
                    ExhaustionPercent:     percent,
                    DaysRemaining:         daysRemaining,
                    IsApproachingEndOfLife: approaching,
                    IsPastEndOfLife:       past);
            })
            .Where(r => r.ExhaustionPercent >= thresholdPercent)
            .OrderByDescending(r => r.ExhaustionPercent)
            .ToList();

        double averageExhaustion = rows.Count == 0
            ? 0.0
            : Math.Round(rows.Average(r => r.ExhaustionPercent), 2);

        int past_count        = rows.Count(r => r.IsPastEndOfLife);
        int approaching_count = rows.Count(r => r.IsApproachingEndOfLife);

        _logger.LogDebug(
            "Lifespan KPI: {Considered} considered, {Approaching} approaching, {Past} past EOL (threshold {Threshold}%)",
            raw.Count, approaching_count, past_count, thresholdPercent);

        return new LifespanExhaustionDto(
            ConsideredAssets:           raw.Count,
            ThresholdPercent:           thresholdPercent,
            AssetsApproachingEndOfLife: approaching_count,
            AssetsPastEndOfLife:        past_count,
            AverageExhaustionPercent:   averageExhaustion,
            Rows:                       rows);
    }
}
