using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.API.Services;

/// <summary>
/// Read-only ITIL analytics built on the existing Equipment table.
/// Distinct from the aggregate KpisController.Get path: this service
/// returns the per-asset detail rows that drive the disposal trigger
/// dashboard card.
/// </summary>
public interface IItilAnalyticsService
{
    Task<LifespanExhaustionDto> GetLifespanExhaustionAsync(
        double thresholdPercent = 90.0,
        CancellationToken ct = default);
}
