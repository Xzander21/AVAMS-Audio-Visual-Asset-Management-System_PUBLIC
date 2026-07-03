using System.Net.Http.Json;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class AcquisitionService
{
    private readonly HttpClient _http;
    public AcquisitionService(HttpClient http) { _http = http; }

    public async Task<List<AcquisitionDto>> GetAllAsync()
        => await _http.GetFromJsonAsync<List<AcquisitionDto>>("api/acquisitions") ?? new();

    public async Task<AcquisitionSummaryClientDto?> GetSummaryAsync()
        => await _http.GetFromJsonAsync<AcquisitionSummaryClientDto>("api/acquisitions/summary");

    public async Task<(AcquisitionDto? Data, string? Error)> CreateAsync(CreateAcquisitionDto dto)
    {
        var r = await _http.PostAsJsonAsync("api/acquisitions", dto);
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<AcquisitionDto>(), null);
        return (null, await r.Content.ReadAsStringAsync());
    }

    public async Task<(AcquisitionDto? Data, string? Error)> UpdateAsync(int id, UpdateAcquisitionDto dto)
    {
        var r = await _http.PutAsJsonAsync($"api/acquisitions/{id}", dto);
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<AcquisitionDto>(), null);
        return (null, await r.Content.ReadAsStringAsync());
    }

    public async Task<(AcquisitionDto? Data, string? Error)> OrderAsync(int id)
        => await PostAction(id, "order");

    public async Task<(AcquisitionDto? Data, string? Error)> ReceiveAsync(int id)
        => await PostAction(id, "receive");

    public async Task<(AcquisitionDto? Data, string? Error)> DeployAsync(int id, DeployAcquisitionDto dto)
    {
        var r = await _http.PostAsJsonAsync($"api/acquisitions/{id}/deploy", dto);
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<AcquisitionDto>(), null);
        return (null, await r.Content.ReadAsStringAsync());
    }

    public async Task<(AcquisitionDto? Data, string? Error)> CancelAsync(int id)
        => await PostAction(id, "cancel");

    private async Task<(AcquisitionDto?, string?)> PostAction(int id, string action)
    {
        var r = await _http.PostAsync($"api/acquisitions/{id}/{action}", null);
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<AcquisitionDto>(), null);
        return (null, await r.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var r = await _http.DeleteAsync($"api/acquisitions/{id}");
        if (r.IsSuccessStatusCode) return (true, null);
        return (false, await r.Content.ReadAsStringAsync());
    }
}

public class AcquisitionSummaryClientDto
{
    public int Total     { get; set; }
    public int Planned   { get; set; }
    public int Ordered   { get; set; }
    public int Received  { get; set; }
    public int Deployed  { get; set; }
    public int Cancelled { get; set; }
}
