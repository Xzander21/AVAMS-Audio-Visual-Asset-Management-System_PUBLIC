using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace AVEquipmentManager.Web.Auth;

/// <summary>
/// Delegating handler that reads the JWT from sessionStorage and attaches it as a
/// Bearer token to every outgoing HTTP request (except the login endpoint itself,
/// which lives on an HttpClient that does NOT use this handler).
///
/// Finding #3 / CWE-922 patch (2026-06-16): storage moved from localStorage to
/// sessionStorage so the token is cleared when the tab closes, narrowing the
/// XSS exfiltration window.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IJSRuntime _js;

    public BearerTokenHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _js.InvokeAsync<string?>(
                "sessionStorage.getItem", cancellationToken, "authToken");

            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            // JS interop may fail during pre-rendering — ignore and continue unauthenticated
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
