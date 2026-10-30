using ClinicBookingSystem.Clinics.Application;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.TimeSlots.Application;
using Mediator;

namespace ClinicBookingSystem.Api.Endpoints;

public static class ClinicEndpoints
{
    public static IEndpointRouteBuilder MapClinicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clinics").WithTags("Clinics");

        group.MapGet("/", async (IMediator m, CancellationToken ct) => Results.Ok(await m.Send(new GetClinicsQuery(), ct)))
            .AllowAnonymous().WithSummary("List active clinics.");

        group.MapGet("/{clinicId:guid}/timeslots", async (Guid clinicId, DateOnly date, IMediator m, CancellationToken ct) =>
            {
                var (from, to) = ClinicTime.DayRange(date);
                return Results.Ok(await m.Send(new GetTimeSlotsQuery(clinicId, from, to), ct));
            })
            .AllowAnonymous().WithSummary("List available time slots for a clinic on a given (clinic-local) day.");

        group.MapPost("/", async (CreateClinicRequest r, IMediator m, CancellationToken ct) =>
            {
                var clinic = await m.Send(new CreateClinicCommand(r.Name, r.Address, r.PhoneNumber), ct);
                return Results.Created($"/api/clinics/{clinic.Id}", clinic);
            })
            .RequireAuthorization(p => p.RequireRole(Roles.Admin)).WithSummary("Create a clinic (admin).");

        app.MapPost("/api/timeslots/generate", async (GenerateTimeSlotsRequest r, IMediator m, CancellationToken ct) =>
            {
                var created = await m.Send(new GenerateTimeSlotsCommand(r.ClinicId, r.OpensAt, r.ClosesAt, r.SlotMinutes), ct);
                return Results.Ok(new GenerateTimeSlotsResponse(created));
            })
            .WithTags("TimeSlots")
            .RequireAuthorization(p => p.RequireRole(Roles.Admin, Roles.ClinicStaff))
            .WithSummary("Open a day of bookable slots (admin or clinic staff).");

        return app;
    }
}
