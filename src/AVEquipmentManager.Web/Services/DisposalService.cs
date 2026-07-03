using System.Net.Http.Json;
using System.Web;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class DisposalService
{
    private readonly HttpClient _http;

    public DisposalService(HttpClient http) { _http = http; }

    public async Task<List<DisposalDto>> GetAllAsync(string? status = null, string? method = null)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
        if (!string.IsNullOrWhiteSpace(method)) q["method"] = method;
        var url = "api/disposals" + (q.Count > 0 ? "?" + q : "");
        return await _http.GetFromJsonAsync<List<DisposalDto>>(url) ?? new List<DisposalDto>();
    }

    public async Task<DisposalDto?> GetByIdAsync(int id)
        => await _http.GetFromJsonAsync<DisposalDto>($"api/disposals/{id}");

    public async Task<DisposalSummaryClientDto?> GetSummaryAsync()
        => await _http.GetFromJsonAsync<DisposalSummaryClientDto>("api/disposals/summary");

    public async Task<(DisposalDto? Data, string? Error)> CreateAsync(CreateDisposalDto dto)
    {
        var resp = await _http.PostAsJsonAsync("api/disposals", dto);
        if (resp.IsSuccessStatusCode) return (await resp.Content.ReadFromJsonAsync<DisposalDto>(), null);
        return (null, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(DisposalDto? Data, string? Error)> UpdateAsync(int id, UpdateDisposalDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"api/disposals/{id}", dto);
        if (resp.IsSuccessStatusCode) return (await resp.Content.ReadFromJsonAsync<DisposalDto>(), null);
        return (null, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(DisposalDto? Data, string? Error)> ApproveAsync(int id)
        => await PostActionAsync(id, "approve");

    /// <summary>
    /// Mark Approved disposal as Disposed. Optionally promote a Reserved
    /// equipment to Active and inherit the disposed unit's room.
    /// </summary>
    public async Task<(DisposalDto? Data, string? Error)> DisposeAsync(
        int id, int? replacementEquipmentId = null)
    {
        var body = new { ReplacementEquipmentId = replacementEquipmentId };
        var resp = await _http.PostAsJsonAsync($"api/disposals/{id}/dispose", body);
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<DisposalDto>(), null);
        return (null, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(DisposalDto? Data, string? Error)> CancelAsync(int id)
        => await PostActionAsync(id, "cancel");

    private async Task<(DisposalDto?, string?)> PostActionAsync(int id, string action)
    {
        var resp = await _http.PostAsync($"api/disposals/{id}/{action}", null);
        if (resp.IsSuccessStatusCode) return (await resp.Content.ReadFromJsonAsync<DisposalDto>(), null);
        return (null, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/disposals/{id}");
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await resp.Content.ReadAsStringAsync());
    }
}

public class DisposalSummaryClientDto
{
    public int Total     { get; set; }
    public int Pending   { get; set; }
    public int Approved  { get; set; }
    public int Disposed  { get; set; }
    public int Cancelled { get; set; }
}
