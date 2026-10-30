using System.Net;
using System.Net.Http.Json;
using ClinicBookingSystem.Contracts;

namespace ClinicBookingSystem.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class BookingApiTests(ApiFixture api)
{
    [Fact]
    public async Task Register_and_login_return_a_token()
    {
        var patient = await api.CreatePatientClientAsync();
        patient.DefaultRequestHeaders.Authorization.ShouldNotBeNull();

        var login = await api.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "admin@phcis.local", Password = "wrong-password" });
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_with_a_weak_password_is_rejected()
    {
        var response = await api.CreateAnonymousClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Weak", LastName = "Password", Email = $"weak-{Guid.NewGuid():N}@example.com",
            PhoneNumber = "0821234567", DateOfBirth = new DateTime(1990, 1, 1), Password = "short"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoints_enforce_authentication_and_roles()
    {
        var anonymous = api.CreateAnonymousClient();
        var patient = await api.CreatePatientClientAsync();

        (await anonymous.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await patient.PostAsJsonAsync("/api/clinics", new CreateClinicRequest("Nope", "Nowhere", "0115550000")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await patient.PostAsJsonAsync("/api/timeslots/generate",
                new GenerateTimeSlotsRequest(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), 30)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Booking_a_slot_confirms_it_and_removes_it_from_the_available_slots()
    {
        var (clinic, slots) = await NewClinicAsync();
        var patient = await api.CreatePatientClientAsync();

        var response = await patient.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slots[0].Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var confirmation = (await response.Content.ReadFromJsonAsync<AppointmentConfirmationDto>())!;
        confirmation.Appointment.Status.ShouldBe("Confirmed");
        confirmation.Message.ShouldContain(confirmation.Appointment.ConfirmationNumber);
        confirmation.Message.ShouldContain(clinic.Name);

        var after = await ApiFixture.GetSlotsAsync(patient, clinic.Id);
        after.ShouldNotContain(s => s.Id == slots[0].Id && s.IsAvailable);

        var mine = await patient.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments");
        mine!.ShouldContain(a => a.Id == confirmation.Appointment.Id);
    }

    [Fact]
    public async Task A_booked_slot_cannot_be_booked_by_another_patient()
    {
        var (_, slots) = await NewClinicAsync();
        var first = await api.CreatePatientClientAsync();
        var second = await api.CreatePatientClientAsync();

        (await first.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slots[0].Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await second.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slots[0].Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_bookings_of_one_slot_produce_exactly_one_winner()
    {
        var (_, slots) = await NewClinicAsync();
        var patients = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => api.CreatePatientClientAsync()));

        var responses = await Task.WhenAll(patients.Select(p =>
            p.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slots[0].Id))));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(patients.Length - 1);
    }

    [Fact]
    public async Task A_patient_cannot_hold_two_appointments_at_the_same_time()
    {
        var (_, firstClinicSlots) = await NewClinicAsync();
        var (_, secondClinicSlots) = await NewClinicAsync();
        var patient = await api.CreatePatientClientAsync();

        (await patient.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(firstClinicSlots[0].Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await patient.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(secondClinicSlots[0].Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_for_someone_else()
    {
        var (clinic, slots) = await NewClinicAsync();
        var first = await api.CreatePatientClientAsync();
        var second = await api.CreatePatientClientAsync();
        var booked = await Book(first, slots[0]);

        (await second.DeleteAsync($"/api/appointments/{booked.Appointment.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await first.DeleteAsync($"/api/appointments/{booked.Appointment.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ApiFixture.GetSlotsAsync(first, clinic.Id)).Single(s => s.Id == slots[0].Id).IsAvailable.ShouldBeTrue();
        (await second.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slots[0].Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rescheduling_moves_the_appointment_and_swaps_the_slots()
    {
        var (clinic, slots) = await NewClinicAsync();
        var patient = await api.CreatePatientClientAsync();
        var booked = await Book(patient, slots[0]);

        var response = await patient.PutAsJsonAsync($"/api/appointments/{booked.Appointment.Id}/reschedule",
            new RescheduleAppointmentRequest(slots[2].Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rescheduled = (await response.Content.ReadFromJsonAsync<AppointmentConfirmationDto>())!;
        rescheduled.Appointment.ConfirmationNumber.ShouldBe(booked.Appointment.ConfirmationNumber);
        rescheduled.Appointment.StartsAt.ShouldBe(slots[2].StartsAt);
        rescheduled.Message.ShouldContain("rescheduled");

        var after = await ApiFixture.GetSlotsAsync(patient, clinic.Id);
        after.Single(s => s.Id == slots[0].Id).IsAvailable.ShouldBeTrue();
        after.ShouldNotContain(s => s.Id == slots[2].Id && s.IsAvailable);
    }

    private async Task<(ClinicDto Clinic, IReadOnlyList<TimeSlotDto> Slots)> NewClinicAsync() =>
        await api.CreateClinicWithSlotsAsync(await api.CreateAdminClientAsync());

    private static async Task<AppointmentConfirmationDto> Book(HttpClient patient, TimeSlotDto slot)
    {
        var response = await patient.PostAsJsonAsync("/api/appointments", new BookAppointmentRequest(slot.Id));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AppointmentConfirmationDto>())!;
    }
}
