using System.Security.Claims;
using ClinicBookingSystem.Appointments.Application;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.Patients.Application;
using Mediator;

namespace ClinicBookingSystem.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments").WithTags("Appointments")
            .RequireAuthorization(p => p.RequireRole(Roles.Patient));

        group.MapGet("/", async (ClaimsPrincipal user, IMediator m, CancellationToken ct) =>
        {
            var patient = await CurrentPatient(user, m, ct);
            return Results.Ok(await m.Send(new GetPatientAppointmentsQuery(patient.Id), ct));
        }).WithSummary("List the signed-in patient's appointments.");

        group.MapPost("/", async (BookAppointmentRequest r, ClaimsPrincipal user, IMediator m, CancellationToken ct) =>
        {
            var patient = await CurrentPatient(user, m, ct);
            var confirmation = await m.Send(new BookAppointmentCommand(patient.Id, r.TimeSlotId), ct);
            return Results.Created($"/api/appointments/{confirmation.Appointment.Id}", confirmation);
        }).WithSummary("Book a time slot. Returns a confirmation message.");

        group.MapPut("/{appointmentId:guid}/reschedule", async (Guid appointmentId, RescheduleAppointmentRequest r,
            ClaimsPrincipal user, IMediator m, CancellationToken ct) =>
        {
            var patient = await CurrentPatient(user, m, ct);
            return Results.Ok(await m.Send(new RescheduleAppointmentCommand(patient.Id, appointmentId, r.NewTimeSlotId), ct));
        }).WithSummary("Move an appointment to another free time slot.");

        group.MapDelete("/{appointmentId:guid}", async (Guid appointmentId, ClaimsPrincipal user, IMediator m, CancellationToken ct) =>
        {
            var patient = await CurrentPatient(user, m, ct);
            await m.Send(new CancelAppointmentCommand(patient.Id, appointmentId), ct);
            return Results.NoContent();
        }).WithSummary("Cancel an appointment and free its slot.");

        return app;
    }

    private static ValueTask<PatientDto> CurrentPatient(ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        mediator.Send(new GetPatientByIdentityUserQuery(user.GetUserId()), ct);
}
