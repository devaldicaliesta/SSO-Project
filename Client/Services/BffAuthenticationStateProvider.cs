using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Client.Services;

/// <summary>
/// Determines the authentication state from the BFF instead of from a token in
/// the browser. It calls the server's <c>/bff/user</c> endpoint, which returns
/// the user's claims (200) when the HttpOnly session cookie is valid, or 401
/// when the user is anonymous. No token is ever read or stored on the client.
/// </summary>
public class BffAuthenticationStateProvider : AuthenticationStateProvider
{
    // Avoid hitting /bff/user on every single render.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private AuthenticationState _cached = Anonymous();

    public BffAuthenticationStateProvider(HttpClient http) => _http = http;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (DateTimeOffset.UtcNow < _lastCheck + CacheDuration &&
            _cached.User.Identity?.IsAuthenticated == true)
        {
            return _cached;
        }

        _cached = new AuthenticationState(await FetchUserAsync());
        _lastCheck = DateTimeOffset.UtcNow;
        return _cached;
    }

    /// <summary>Forces a re-fetch and notifies the UI (e.g. after login/logout).</summary>
    public void Invalidate()
    {
        _lastCheck = DateTimeOffset.MinValue;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<ClaimsPrincipal> FetchUserAsync()
    {
        try
        {
            // The X-CSRF header required by the BFF is added by AntiforgeryHandler.
            using var response = await _http.GetAsync("bff/user");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var claims = await response.Content.ReadFromJsonAsync<List<ClaimRecord>>();
                if (claims is { Count: > 0 })
                {
                    var identity = new ClaimsIdentity(
                        authenticationType: "bff",
                        nameType: "name",
                        roleType: "role");
                    identity.AddClaims(claims.Select(c => new Claim(c.Type, c.Value)));
                    return new ClaimsPrincipal(identity);
                }
            }
        }
        catch
        {
            // Network error or not signed in -> treat as anonymous.
        }

        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static AuthenticationState Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    // Shape of each element returned by /bff/user: { "type": "...", "value": "..." }.
    private sealed record ClaimRecord(string Type, string Value);
}
