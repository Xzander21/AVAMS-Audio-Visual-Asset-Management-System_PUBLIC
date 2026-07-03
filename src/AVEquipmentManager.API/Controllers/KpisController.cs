using AVEquipmentManager.API.Data;
using AVEquipmentManager.API.Services;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVEquipmentManager.API.Controllers;

/// <summary>
/// Computed Key Performance Indicators for AVAMS. Values are derived from
/// the underlying Equipment, Ticket, Disposal, and Acquisition entities at
/// request time — nothing is persisted in this endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KpisController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IItilAnalyticsService _analytics;
    public KpisController(AppDbContext context, IItilAnalyticsService analytics)
    {
        _context   = context;
        _analytics = analytics;
    }

    /// <summary>
    /// Per-asset lifespan-exhaustion detail. Complements the aggregate
    /// LifecycleCompliancePercent / AtRiskAssets values returned by Get().
    /// </summary>
    [HttpGet("lifespan-exhaustion")]
    public Task<LifespanExhaustionDto> LifespanExhaustion(
        [FromQuery] double threshold = 90.0, CancellationToken ct = default)
        => _analytics.GetLifespanExhaustionAsync(threshold, ct);

    [HttpGet]
    public async Task<ActionResult<KpiDto>> Get()
    {
        var equipment = await _context.Equipment.Where(e => !e.IsArchived).ToListAsync();
        var tickets   = await _context.Tickets.ToListAsync();
        var disposals = await _context.Disposals.ToListAsync();
        var acquisitions = await _context.Acquisitions.ToListAsync();

        var now = DateTime.UtcNow;
        var d30 = now.AddDays(-30);
        var d90 = now.AddDays(-90);
        var d180 = now.AddDays(180);

        var total = equipment.Count;
        var active = equipment.Count(e => e.Status == EquipmentStatus.Active);
        var underMaint = equipment.Count(e => e.Status == EquipmentStatus.UnderMaintenance);

        // Lifecycle compliance: % of active equipment whose expected EoL is in the future
        var compliant = equipment.Count(e =>
            e.Status != EquipmentStatus.Retired &&
            e.Status != EquipmentStatus.Decommissioned &&
            e.DateInstalled.AddYears(e.ExpectedLifeInYears) > now);

        // At-risk: expired OR expiring within 180 days
        var atRisk = equipment.Count(e =>
        {
            if (e.Status == EquipmentStatus.Retired || e.Status == EquipmentStatus.Decommissioned) return false;
            var eol = e.DateInstalled.AddYears(e.ExpectedLifeInYears);
            return eol <= d180;
        });

        // MTBF — for equipment items with 2+ maintenance records, average days between consecutive records
        var ticketsByEq = tickets.GroupBy(t => t.EquipmentId).Where(g => g.Count() >= 2);
        var intervals = new List<double>();
        foreach (var g in ticketsByEq)
        {
            var ordered = g.OrderBy(t => t.CreatedAt).ToList();
            for (int i = 1; i < ordered.Count; i++)
                intervals.Add((ordered[i].CreatedAt - ordered[i - 1].CreatedAt).TotalDays);
        }
        var mtbf = intervals.Count > 0 ? Math.Round(intervals.Average(), 1) : -1;

        // MTTR — average ticket lifetime from CreatedAt to ResolvedAt
        var resolved = tickets.Where(t => t.ResolvedAt.HasValue).ToList();
        var mttr = resolved.Count > 0
            ? Math.Round(resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalDays), 1)
            : -1;

        var kpi = new KpiDto
        {
            TotalAssets                 = total,
            AvailabilityPercent         = total == 0 ? 0 : Math.Round(100.0 * active / total, 1),
            UtilizationPercent          = total == 0 ? 0 : Math.Round(100.0 * (active + underMaint) / total, 1),
            LifecycleCompliancePercent  = total == 0 ? 0 : Math.Round(100.0 * compliant / total, 1),
            MeanTimeBetweenFailuresDays = mtbf,
            MeanTimeToRepairDays        = mttr,
            MaintenanceBacklog          = tickets.Count(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress),
            ResolvedLast30Days          = tickets.Count(t => t.ResolvedAt.HasValue && t.ResolvedAt >= d30),
            DisposedLast90Days          = disposals.Count(d => d.DisposedAt.HasValue && d.DisposedAt >= d90),
            AcquiredLast90Days          = acquisitions.Count(a => a.DeployedAt.HasValue && a.DeployedAt >= d90),
            AtRiskAssets                = atRisk,
            ComputedAt                  = now
        };

        return Ok(kpi);
    }
}
