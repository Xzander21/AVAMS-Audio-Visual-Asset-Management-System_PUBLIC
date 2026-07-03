using System.Security.Claims;
using System.Text.Json;
using AVEquipmentManager.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace AVEquipmentManager.Web.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;

    private const string TokenKey = "authToken";

    // Fully unauthenticated state (cached for reuse)
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public CustomAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? token;
        try
        {
            token = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        }
        catch
        {
            // JS interop not available (pre-render or SSR context)
            return Anonymous;
        }

        if (string.IsNullOrWhiteSpace(token))
            return Anonymous;

        IEnumerable<Claim> claims;
        try { claims = ParseClaimsFromJwt(token); }
        catch { return Anonymous; }

        // Check token expiry
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            if (expiry < DateTime.UtcNow)
            {
                // Token expired — clear it silently
                try { await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey); }
                catch { }
                return Anonymous;
            }
        }

        // Build identity — specify the claim types used for name and role
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "jwt",
            nameType: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            roleType: "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Call this after login or logout to push a new auth state to all subscribers.
    /// </summary>
    public void NotifyStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    // ── JWT payload parser (no external library required) ─────────────────────

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException("Invalid JWT format");

        var json = Base64UrlDecode(parts[1]);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? throw new InvalidOperationException("Empty JWT payload");

        var claims = new List<Claim>();
        foreach (var (key, element) in dict)
        {
            // A claim value can be a scalar or an array (multiple roles, etc.)
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    claims.Add(new Claim(key, item.GetString() ?? string.Empty));
            }
            else
            {
                claims.Add(new Claim(key, element.ToString()));
            }
        }
        return claims;
    }

    private static string Base64UrlDecode(string base64Url)
    {
        // Pad and convert Base64Url → Base64
        base64Url = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64Url.Length % 4)
        {
            case 2: base64Url += "=="; break;
            case 3: base64Url += "=";  break;
        }
        var bytes = Convert.FromBase64String(base64Url);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
