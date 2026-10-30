using Shouldly;

namespace ClinicBookingSystem.E2ETests;

public class BookingFlowTests : E2ETestBase
{
    [Fact]
    public async Task Patient_CanBookASlot_AndSeesConfirmationMessage()
    {
        await SignInAsync(await RegisterPatientAsync());
        await Page.GotoAsync($"{WebUrl}/book");

        await PickFirstClinicAsync();
        await Page.GetByTestId("slot").First.ClickAsync();
        await Page.GetByTestId("book-submit").ClickAsync();

        var message = Page.GetByTestId("confirmation-message");
        await Expect(message).ToContainTextAsync("has been confirmed");
        (await message.InnerTextAsync()).ShouldContain("APT-");
    }

    [Fact]
    public async Task BookedSlot_IsNoLongerOfferedToAnotherPatient()
    {
        await SignInAsync(await RegisterPatientAsync());
        await Page.GotoAsync($"{WebUrl}/book");
        await PickFirstClinicAsync();
        await Expect(Page.GetByTestId("slot").First).ToBeVisibleAsync();
        var slotsBefore = await Page.GetByTestId("slot").CountAsync();
        var bookedTime = await Page.GetByTestId("slot").First.InnerTextAsync();
        await Page.GetByTestId("slot").First.ClickAsync();
        await Page.GetByTestId("book-submit").ClickAsync();
        await Expect(Page.GetByTestId("confirmation-message")).ToBeVisibleAsync();

        // A second patient looks at the same clinic and day.
        await Page.GetByTestId("nav-logout").ClickAsync();
        await SignInAsync(await RegisterPatientAsync());
        await Page.GotoAsync($"{WebUrl}/book");
        await PickFirstClinicAsync();
        await Expect(Page.GetByTestId("slot").First).ToBeVisibleAsync();

        (await Page.GetByTestId("slot").CountAsync()).ShouldBe(slotsBefore - 1);
        (await Page.GetByTestId("slot").AllInnerTextsAsync()).ShouldNotContain(bookedTime);
    }

    [Fact]
    public async Task Patient_CanCancelAnAppointment()
    {
        await SignInAsync(await RegisterPatientAsync());
        await Page.GotoAsync($"{WebUrl}/book");
        await PickFirstClinicAsync();
        await Page.GetByTestId("slot").First.ClickAsync();
        await Page.GetByTestId("book-submit").ClickAsync();
        await Expect(Page.GetByTestId("confirmation-message")).ToBeVisibleAsync();

        await Page.GotoAsync($"{WebUrl}/appointments");
        await Page.GetByTestId("cancel").ClickAsync();
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Cancel appointment" }).ClickAsync();

        await Expect(Page.GetByTestId("appointments-table").GetByText("Cancelled")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Register_WithEmptyForm_ShowsValidationErrors()
    {
        await Page.GotoAsync($"{WebUrl}/register");

        await Page.GetByTestId("register-submit").ClickAsync();

        await Expect(Page.GetByText("'First Name' must not be empty.")).ToBeVisibleAsync();
    }
}
