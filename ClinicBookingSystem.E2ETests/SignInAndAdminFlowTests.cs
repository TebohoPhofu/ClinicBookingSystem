using Shouldly;

namespace ClinicBookingSystem.E2ETests;

/// <summary>Sign-in routing per role, and the clinic-staff/admin stakeholder flows from the requirements.</summary>
public class SignInAndAdminFlowTests : E2ETestBase
{
    private const string AdminEmail = "admin@phcis.local";
    private const string AdminPassword = "Admin#12345";

    [Fact]
    public async Task Patient_SigningIn_LandsOnTheBookingPage()
    {
        await SignInAsync(await RegisterPatientAsync());

        await Expect(Page).ToHaveURLAsync($"{WebUrl}/book");
        await Expect(Page.GetByTestId("clinic-select")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_SigningIn_LandsOnTheManagementPage_NotBackOnLogin()
    {
        await SignInAsync(AdminEmail, AdminPassword);

        await Expect(Page).ToHaveURLAsync($"{WebUrl}/manage");
        await Expect(Page.GetByTestId("manage-generate")).ToBeVisibleAsync();
        (await Page.GetByTestId("nav-book").CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Patient_OpeningTheManagementPage_SeesAccessDenied_NotTheLoginPage()
    {
        await SignInAsync(await RegisterPatientAsync());

        await Page.GotoAsync($"{WebUrl}/manage");

        await Expect(Page.GetByTestId("access-denied")).ToBeVisibleAsync();
        Page.Url.ShouldNotContain("/login");
    }

    [Fact]
    public async Task Anonymous_OpeningTheBookingPage_IsSentToLogin()
    {
        await Page.GotoAsync($"{WebUrl}/book");

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Fact]
    public async Task Admin_CanAddAClinic()
    {
        await SignInAsync(AdminEmail, AdminPassword);
        var name = $"E2E Clinic {Guid.NewGuid():N}"[..24];

        await Field("clinic-name").FillAsync(name);
        await Field("clinic-address").FillAsync("1 Test Street");
        await Field("clinic-phone").FillAsync("0115550000");
        await Page.GetByTestId("clinic-create").ClickAsync();

        await Expect(Page.GetByText($"Clinic '{name}' added")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task WrongPassword_ShowsAnError_AndStaysOnLogin()
    {
        await Page.GotoAsync($"{WebUrl}/login");
        await Field("login-email").FillAsync(AdminEmail);
        await Field("login-password").FillAsync("Wrong#Password1");
        await Page.GetByTestId("login-submit").ClickAsync();

        await Expect(Page.GetByTestId("login-error")).ToBeVisibleAsync();
    }
}
