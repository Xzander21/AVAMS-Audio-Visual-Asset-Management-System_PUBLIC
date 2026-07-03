using System.Net.Http.Json;
using System.Web;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class StaffService
{
    private readonly HttpClient _http;

    public StaffService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<StaffDto>> GetAllAsync(string? search = null, string? room = null, string? department = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(search))     query["search"]     = search;
        if (!string.IsNullOrWhiteSpace(room))       query["room"]       = room;
        if (!string.IsNullOrWhiteSpace(department)) query["department"] = department;
        var url = "api/staff" + (query.Count > 0 ? "?" + query : "");
        return await _http.GetFromJsonAsync<List<StaffDto>>(url) ?? new List<StaffDto>();
    }

    public async Task<StaffDto?> GetByIdAsync(int id)
        => await _http.GetFromJsonAsync<StaffDto>($"api/staff/{id}");

    public async Task<StaffTrackingDto?> TrackAsync(int id)
        => await _http.GetFromJsonAsync<StaffTrackingDto>($"api/staff/{id}/tracking");

    public async Task<List<string>> GetDepartmentsAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/staff/departments") ?? new List<string>();

    public async Task<(StaffDto? Data, string? Error)> CreateAsync(CreateStaffDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/staff", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<StaffDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(StaffDto? Data, string? Error)> UpdateAsync(int id, UpdateStaffDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/staff/{id}", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<StaffDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/staff/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }

    public async Task<List<StaffDto>> GetArchivedAsync()
        => await _http.GetFromJsonAsync<List<StaffDto>>("api/staff/archive") ?? new List<StaffDto>();

    public async Task<(StaffDto? Data, string? Error)> RestoreAsync(int id)
    {
        var response = await _http.PostAsync($"api/staff/{id}/restore", null);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<StaffDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(bool Success, string? Error)> PurgeAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/staff/{id}/purge");
        if (response.IsSuccessStatusCode) return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }
}
