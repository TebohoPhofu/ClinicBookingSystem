using System.Security.Claims;
using System.Text.Json;
using ClinicBookingSystem.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace ClinicBookingSystem.BlazorWasm.Services;

/// <summary>
/// Keeps the JWT in browser localStorage and exposes it as the Blazor authentication state.
/// Registered as a singleton so the HTTP message handler and the UI see the same instance.
/// </summary>
public sealed class JwtAuthStateProvider(IJSRuntime js) : AuthenticationStateProvider
{
    private const string StorageKey = "phcis.auth";
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthResponse? _session;
    private bool _loaded;

    public async Task<string?> GetTokenAsync()
    {
        await EnsureLoadedAsync();
        return _session?.Token;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureLoadedAsync();
        return _session is null ? Anonymous : new AuthenticationState(CreatePrincipal(_session.Token));
    }

    public async Task SignInAsync(AuthResponse session)
    {
        _session = session;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(session));
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        _session = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;

        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var stored = JsonSerializer.Deserialize<AuthResponse>(json);
            if (stored is not null && stored.ExpiresAt > DateTimeOffset.UtcNow) _session = stored;
        }
        catch (JsonException)
        {
            // A corrupt entry is treated as signed out.
        }
    }

    private static ClaimsPrincipal CreatePrincipal(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));

        var claims = new List<Claim>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
                claims.AddRange(property.Value.EnumerateArray().Select(v => new Claim(property.Name, v.ToString())));
            else
                claims.Add(new Claim(property.Name, property.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
    }
}
