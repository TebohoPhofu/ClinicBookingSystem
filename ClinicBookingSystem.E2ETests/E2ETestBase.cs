using System.Net.Http.Json;
using ClinicBookingSystem.Contracts;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace ClinicBookingSystem.E2ETests;

/// <summary>Shared helpers: URLs of the running system and quick test-user creation through the API.</summary>
public abstract class E2ETestBase : PageTest
{
    protected static readonly string WebUrl = Environment.GetEnvironmentVariable("E2E_WEB_URL") ?? "http://localhost:5271";
    protected static readonly string ApiUrl = Environment.GetEnvironmentVariable("E2E_API_URL") ?? "http://localhost:5100";
    protected const string Password = "Passw0rd1";

    public override BrowserNewContextOptions ContextOptions() => new() { ViewportSize = new() { Width = 1280, Height = 900 } };

    /// <summary>Creates a unique patient through the API and returns its email.</summary>
    protected static async Task<string> RegisterPatientAsync()
    {
        var email = $"e2e.{Guid.NewGuid():N}@example.com";
        using var http = new HttpClient { BaseAddress = new Uri(ApiUrl) };
        var response = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest
        {
            FirstName = "E2E", LastName = "Patient", Email = email, PhoneNumber = "0821234567",
            DateOfBirth = new DateTime(1990, 1, 1), Password = Password
        });
        response.EnsureSuccessStatusCode();
        return email;
    }

    /// <summary>MudBlazor may place data-testid on the wrapper or on the inner input depending on the component.</summary>
    protected ILocator Field(string testId) => Page.Locator($"input[data-testid='{testId}'], [data-testid='{testId}'] input").First;

    protected async Task SignInAsync(string email, string password = Password)
    {
        await Page.GotoAsync($"{WebUrl}/login");
        await Field("login-email").FillAsync(email);
        await Field("login-password").FillAsync(password);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Expect(Page.GetByTestId("nav-logout")).ToBeVisibleAsync();
    }

    /// <summary>Picks a seeded demo clinic, which always has open slots (clinics created by other tests do not).</summary>
    protected async Task PickFirstClinicAsync()
    {
        await Page.GetByTestId("clinic-select").ClickAsync();
        await Page.Locator(".mud-popover-open .mud-list-item", new() { HasText = "Soweto Community Clinic" }).First.ClickAsync();
    }
}
