using System.Net.Http.Json;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class KpiService
{
    private readonly HttpClient _http;
    public KpiService(HttpClient http) { _http = http; }

    public async Task<KpiDto?> GetAsync()
        => await _http.GetFromJsonAsync<KpiDto>("api/kpis");

    /// <summary>
    /// Per-asset lifespan-exhaustion detail. Pass a threshold (default 90)
    /// to control which assets are included in the result list.
    /// </summary>
    public Task<LifespanExhaustionDto?> GetLifespanExhaustionAsync(double threshold = 90.0)
        => _http.GetFromJsonAsync<LifespanExhaustionDto>(
            $"api/kpis/lifespan-exhaustion?threshold={threshold}");
}
