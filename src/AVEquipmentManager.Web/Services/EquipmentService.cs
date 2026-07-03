using System.Net.Http.Json;
using System.Web;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class EquipmentService
{
    private readonly HttpClient _http;

    public EquipmentService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<EquipmentDto>> GetAllAsync(string? room = null, string? status = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(room)) query["room"] = room;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        var url = "api/equipment" + (query.Count > 0 ? "?" + query : "");
        return await _http.GetFromJsonAsync<List<EquipmentDto>>(url) ?? new List<EquipmentDto>();
    }

    public async Task<EquipmentDto?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<EquipmentDto>($"api/equipment/{id}");
    }

    public async Task<List<string>> GetRoomsAsync()
    {
        return await _http.GetFromJsonAsync<List<string>>("api/equipment/rooms") ?? new List<string>();
    }

    public async Task<(EquipmentDto? Data, string? Error)> CreateAsync(CreateEquipmentDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/equipment", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<EquipmentDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(EquipmentDto? Data, string? Error)> UpdateAsync(int id, UpdateEquipmentDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/equipment/{id}", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<EquipmentDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/equipment/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }

    public async Task<List<EquipmentDto>> GetArchivedAsync()
        => await _http.GetFromJsonAsync<List<EquipmentDto>>("api/equipment/archive") ?? new List<EquipmentDto>();

    public async Task<(EquipmentDto? Data, string? Error)> RestoreAsync(int id)
    {
        var response = await _http.PostAsync($"api/equipment/{id}/restore", null);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<EquipmentDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> PurgeAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/equipment/{id}/purge");
        if (response.IsSuccessStatusCode) return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }
}
