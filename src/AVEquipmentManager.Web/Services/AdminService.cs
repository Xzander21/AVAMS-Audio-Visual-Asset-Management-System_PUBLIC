using System.Net.Http.Json;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

/// <summary>
/// Wraps Admin-only API endpoints: user listing, role changes, and user deletion.
/// Uses the bearer-token-enabled HttpClient registered in Program.cs.
/// </summary>
public class AdminService
{
    private readonly HttpClient _http;

    public AdminService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<UserDto>> GetUsersAsync()
        => await _http.GetFromJsonAsync<List<UserDto>>("api/auth/users") ?? new();

    public async Task<(bool Success, string? Error)> RegisterAsync(
        string username, string email, string password, string role)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register",
            new { username, email, password, role });
        if (response.IsSuccessStatusCode) return (true, null);
        var body = await response.Content.ReadAsStringAsync();
        return (false, ExtractMessage(body));
    }

    public async Task<(bool Success, string? Error)> ChangeRoleAsync(int userId, string role)
    {
        var response = await _http.PutAsJsonAsync($"api/auth/users/{userId}/role",
            new { role });
        if (response.IsSuccessStatusCode) return (true, null);
        var body = await response.Content.ReadAsStringAsync();
        return (false, ExtractMessage(body));
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(int userId)
    {
        var response = await _http.DeleteAsync($"api/auth/users/{userId}");
        if (response.IsSuccessStatusCode) return (true, null);
        var body = await response.Content.ReadAsStringAsync();
        return (false, ExtractMessage(body));
    }

    private static string? ExtractMessage(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { }
        return json;
    }
}
