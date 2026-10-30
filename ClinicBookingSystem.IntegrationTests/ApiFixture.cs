using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicBookingSystem.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

namespace ClinicBookingSystem.IntegrationTests;

/// <summary>
/// Starts one SQL Server container and one in-process API for the whole test collection.
/// Tests stay independent by creating their own clinics and patients.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string AdminEmail = "admin@phcis.local";
    private const string AdminPassword = "Admin#12345";
    public const string PatientPassword = "Passw0rd1";

    // Fresh SQL Server containers need several minutes to start on slower machines. Set TESTCONTAINERS_REUSE=true to
    // keep one container alive between runs; each run still gets its own database, so tests stay isolated.
    private static readonly bool ReuseContainer =
        string.Equals(Environment.GetEnvironmentVariable("TESTCONTAINERS_REUSE"), "true", StringComparison.OrdinalIgnoreCase);

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithReuse(ReuseContainer)
        .Build();
    private WebApplicationFactory<Program>? _factory;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();

        // Program reads its configuration while building the host, so environment variables are the reliable override.
        Environment.SetEnvironmentVariable("ConnectionStrings__ClinicDb", _sql.GetConnectionString() + $";Database=ClinicIntegrationTests_{Guid.NewGuid():N};Command Timeout=300");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-signing-key-0123456789-abcdef");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);

        // The Development environment migrates the database and seeds the admin account.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        _ = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public HttpClient CreateAnonymousClient() => _factory!.CreateClient();

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = AdminEmail, Password = AdminPassword });
        response.EnsureSuccessStatusCode();
        return Authorize(client, await response.Content.ReadFromJsonAsync<AuthResponse>());
    }

    public async Task<HttpClient> CreatePatientClientAsync()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Test",
            LastName = "Patient",
            Email = $"patient-{Guid.NewGuid():N}@example.com",
            PhoneNumber = "0821234567",
            DateOfBirth = new DateTime(1990, 5, 17),
            Password = PatientPassword
        });
        response.EnsureSuccessStatusCode();
        return Authorize(client, await response.Content.ReadFromJsonAsync<AuthResponse>());
    }

    /// <summary>Creates a clinic and opens <paramref name="slotCount"/> consecutive 30-minute slots, starting at 08:00 two weeks from now.</summary>
    public async Task<(ClinicDto Clinic, IReadOnlyList<TimeSlotDto> Slots)> CreateClinicWithSlotsAsync(HttpClient admin, int slotCount = 4)
    {
        var created = await admin.PostAsJsonAsync("/api/clinics", new CreateClinicRequest($"Clinic {Guid.NewGuid():N}", "1 Test Street", "0115550000"));
        created.EnsureSuccessStatusCode();
        var clinic = (await created.Content.ReadFromJsonAsync<ClinicDto>())!;

        var opensAt = SlotStart(0);
        var generated = await admin.PostAsJsonAsync("/api/timeslots/generate",
            new GenerateTimeSlotsRequest(clinic.Id, opensAt, opensAt.AddMinutes(30 * slotCount), 30));
        generated.EnsureSuccessStatusCode();

        return (clinic, await GetSlotsAsync(admin, clinic.Id));
    }

    public static async Task<IReadOnlyList<TimeSlotDto>> GetSlotsAsync(HttpClient client, Guid clinicId)
    {
        var day = DateOnly.FromDateTime(SlotStart(0).DateTime);
        var slots = await client.GetFromJsonAsync<List<TimeSlotDto>>($"/api/clinics/{clinicId}/timeslots?date={day:yyyy-MM-dd}");
        return slots!.OrderBy(s => s.StartsAt).ToList();
    }

    private static DateTimeOffset SlotStart(int slotIndex) =>
        new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(14).AddHours(8), DateTimeKind.Unspecified), ClinicTime.Offset)
            .AddMinutes(30 * slotIndex);

    private static HttpClient Authorize(HttpClient client, AuthResponse? auth)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
