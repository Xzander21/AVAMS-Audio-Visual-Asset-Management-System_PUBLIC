using System.Net.Http.Json;
using System.Web;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class TicketService
{
    private readonly HttpClient _http;

    public TicketService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TicketDto>> GetAllAsync(
        string? status = null,
        string? priority = null,
        string? type = null,
        int? equipmentId = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(status))   query["status"]      = status;
        if (!string.IsNullOrWhiteSpace(priority))  query["priority"]    = priority;
        if (!string.IsNullOrWhiteSpace(type))      query["type"]        = type;
        if (equipmentId.HasValue)                  query["equipmentId"] = equipmentId.Value.ToString();

        var url = "api/tickets" + (query.Count > 0 ? "?" + query : "");
        return await _http.GetFromJsonAsync<List<TicketDto>>(url) ?? new List<TicketDto>();
    }

    public async Task<TicketDto?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<TicketDto>($"api/tickets/{id}");
    }

    public async Task<TicketSummaryDto?> GetSummaryAsync()
    {
        return await _http.GetFromJsonAsync<TicketSummaryDto>("api/tickets/summary");
    }

    public async Task<(TicketDto? Data, string? Error)> CreateAsync(CreateTicketDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/tickets", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TicketDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(TicketDto? Data, string? Error)> UpdateAsync(int id, UpdateTicketDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/tickets/{id}", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TicketDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/tickets/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }

    // ── Transaction-proof lifecycle endpoints ─────────────────────────────

    public async Task<(TicketDto? Data, string? Error)> AcknowledgeAsync(int id, string assignedTo)
    {
        var response = await _http.PostAsJsonAsync($"api/tickets/{id}/acknowledge",
            new { AssignedTo = assignedTo });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TicketDto>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<(TicketDto? Data, string? Error)> ResolveAsync(int id, string resolution)
    {
        var response = await _http.PostAsJsonAsync($"api/tickets/{id}/resolve",
            new { Resolution = resolution });
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TicketDto>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }

    public async Task<(TicketDto? Data, string? Error)> CloseAsync(int id)
    {
        var response = await _http.PostAsync($"api/tickets/{id}/close", null);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TicketDto>(), null);
        return (null, await response.Content.ReadAsStringAsync());
    }
}

// Lightweight summary DTO used only by the front-end
public class TicketSummaryDto
{
    public int Total      { get; set; }
    public int Open       { get; set; }
    public int InProgress { get; set; }
    public int Resolved   { get; set; }
    public int Closed     { get; set; }
    public int Critical   { get; set; }
}
