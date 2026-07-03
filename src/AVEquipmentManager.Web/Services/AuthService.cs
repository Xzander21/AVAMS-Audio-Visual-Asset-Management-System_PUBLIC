using System.Net.Http.Json;
using System.Text.Json;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace AVEquipmentManager.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly AuthenticationStateProvider _authStateProvider;

    private const string TokenKey = "authToken";
    private const string UserKey  = "authUser";

    public AuthService(HttpClient http, IJSRuntime js, AuthenticationStateProvider authStateProvider)
    {
        _http              = http;
        _js                = js;
        _authStateProvider = authStateProvider;
    }

    // ── Login ──────────────────────────────────────────────────────────────────
    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login",
                new { username, password });

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                // Try to extract message field
                try
                {
                    using var doc = JsonDocument.Parse(errBody);
                    if (doc.RootElement.TryGetProperty("message", out var msg))
                        return (false, msg.GetString());
                }
                catch { }
                return (false, "Invalid username or password.");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            if (result is null)
                return (false, "Unexpected response from server.");

            // Persist to sessionStorage (Finding #3 / CWE-922 patch: sessionStorage is
            // tab-scoped and cleared on tab close, narrowing the XSS exfiltration window
            // versus the prior localStorage. The token is still JS-readable — for full
            // protection an HttpOnly cookie + BFF pattern is the next-iteration upgrade.
            // See SECURITY.md for the long-form note.).
            await _js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, result.Token);
            await _js.InvokeVoidAsync("sessionStorage.setItem", UserKey,
                JsonSerializer.Serialize(result));

            // Notify auth state
            ((CustomAuthStateProvider)_authStateProvider).NotifyStateChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Logout ─────────────────────────────────────────────────────────────────
    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", UserKey);
        ((CustomAuthStateProvider)_authStateProvider).NotifyStateChanged();
    }

    // ── Token helpers ──────────────────────────────────────────────────────────
    public async Task<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);

    public async Task<LoginResponseDto?> GetCurrentUserAsync()
    {
        var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", UserKey);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<LoginResponseDto>(json); }
        catch { return null; }
    }
}
