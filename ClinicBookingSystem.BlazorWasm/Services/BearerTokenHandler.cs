using System.Net.Http.Headers;

namespace ClinicBookingSystem.BlazorWasm.Services;

/// <summary>Attaches the signed-in user's JWT to every API request.</summary>
public sealed class BearerTokenHandler(JwtAuthStateProvider auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await auth.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, ct);
    }
}
