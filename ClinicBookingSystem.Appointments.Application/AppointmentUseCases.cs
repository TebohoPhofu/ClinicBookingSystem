using ClinicBookingSystem.Appointments.Domain;
using ClinicBookingSystem.Clinics.Application;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Application;
using FluentValidation;

namespace ClinicBookingSystem.Appointments.Application;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Appointment>> ListForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>True when the patient already holds a confirmed appointment overlapping the window.</summary>
    Task<bool> HasOverlappingAsync(Guid patientId, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? excludeAppointmentId, CancellationToken ct);
    Task AddAsync(Appointment appointment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

internal static class AppointmentMapping
{
    public static AppointmentDto ToDto(this Appointment a, string clinicName) =>
        new(a.Id, a.ClinicId, clinicName, a.StartsAt, a.EndsAt, a.Status.ToString(), a.ConfirmationNumber);

    public static string ConfirmationMessage(this AppointmentDto a, string verb) =>
        $"Your appointment at {a.ClinicName} has been {verb} for " +
        $"{ClinicTime.ToClinicTime(a.StartsAt):dddd, d MMMM yyyy 'at' HH:mm}. Confirmation number: {a.ConfirmationNumber}.";
}

public sealed record BookAppointmentCommand(Guid PatientId, Guid TimeSlotId) : ICommand<AppointmentConfirmationDto>;

public sealed class BookAppointmentCommandValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.TimeSlotId).NotEmpty();
    }
}

public sealed class BookAppointmentHandler(IAppointmentRepository appointments, IMediator mediator, TimeProvider clock)
    : ICommandHandler<BookAppointmentCommand, AppointmentConfirmationDto>
{
    public async ValueTask<AppointmentConfirmationDto> Handle(BookAppointmentCommand command, CancellationToken ct)
    {
        var slot = await mediator.Send(new GetTimeSlotQuery(command.TimeSlotId), ct);

        if (await appointments.HasOverlappingAsync(command.PatientId, slot.StartsAt, slot.EndsAt, null, ct))
            throw new ConflictException("You already have an appointment at this time.");

        // Claim the slot first: its concurrency token guarantees only one caller wins.
        await mediator.Send(new ReserveTimeSlotCommand(slot.Id), ct);

        try
        {
            var appointment = Appointment.Book(command.PatientId, slot.ClinicId, slot.Id, slot.StartsAt, slot.EndsAt, clock.GetUtcNow());
            await appointments.AddAsync(appointment, ct);
            await appointments.SaveChangesAsync(ct);

            var clinic = await mediator.Send(new GetClinicQuery(slot.ClinicId), ct);
            var dto = appointment.ToDto(clinic.Name);
            return new AppointmentConfirmationDto(dto, dto.ConfirmationMessage("confirmed"));
        }
        catch
        {
            // Compensate so a failed save never leaves the slot locked.
            await mediator.Send(new ReleaseTimeSlotCommand(slot.Id), CancellationToken.None);
            throw;
        }
    }
}

public sealed record CancelAppointmentCommand(Guid PatientId, Guid AppointmentId) : ICommand;

public sealed class CancelAppointmentHandler(IAppointmentRepository appointments, IMediator mediator, TimeProvider clock)
    : ICommandHandler<CancelAppointmentCommand>
{
    public async ValueTask<Unit> Handle(CancelAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(command.AppointmentId, ct);
        if (appointment is null || appointment.PatientId != command.PatientId)
            throw new NotFoundException("Appointment not found.");

        appointment.Cancel(clock.GetUtcNow());
        await appointments.SaveChangesAsync(ct);
        await mediator.Send(new ReleaseTimeSlotCommand(appointment.TimeSlotId), ct);
        return Unit.Value;
    }
}

public sealed record RescheduleAppointmentCommand(Guid PatientId, Guid AppointmentId, Guid NewTimeSlotId)
    : ICommand<AppointmentConfirmationDto>;

public sealed class RescheduleAppointmentHandler(IAppointmentRepository appointments, IMediator mediator, TimeProvider clock)
    : ICommandHandler<RescheduleAppointmentCommand, AppointmentConfirmationDto>
{
    public async ValueTask<AppointmentConfirmationDto> Handle(RescheduleAppointmentCommand command, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(command.AppointmentId, ct);
        if (appointment is null || appointment.PatientId != command.PatientId)
            throw new NotFoundException("Appointment not found.");

        if (appointment.TimeSlotId == command.NewTimeSlotId)
            throw new DomainException("Choose a different time slot to reschedule.");

        var newSlot = await mediator.Send(new GetTimeSlotQuery(command.NewTimeSlotId), ct);
        if (await appointments.HasOverlappingAsync(command.PatientId, newSlot.StartsAt, newSlot.EndsAt, appointment.Id, ct))
            throw new ConflictException("You already have an appointment at this time.");

        var oldSlotId = appointment.TimeSlotId;
        await mediator.Send(new ReserveTimeSlotCommand(newSlot.Id), ct);

        try
        {
            appointment.Reschedule(newSlot.Id, newSlot.ClinicId, newSlot.StartsAt, newSlot.EndsAt, clock.GetUtcNow());
            await appointments.SaveChangesAsync(ct);
        }
        catch
        {
            await mediator.Send(new ReleaseTimeSlotCommand(newSlot.Id), CancellationToken.None);
            throw;
        }

        await mediator.Send(new ReleaseTimeSlotCommand(oldSlotId), ct);

        var clinic = await mediator.Send(new GetClinicQuery(newSlot.ClinicId), ct);
        var dto = appointment.ToDto(clinic.Name);
        return new AppointmentConfirmationDto(dto, dto.ConfirmationMessage("rescheduled"));
    }
}

public sealed record GetPatientAppointmentsQuery(Guid PatientId) : IQuery<IReadOnlyList<AppointmentDto>>;

public sealed class GetPatientAppointmentsHandler(IAppointmentRepository appointments, IMediator mediator)
    : IQueryHandler<GetPatientAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    public async ValueTask<IReadOnlyList<AppointmentDto>> Handle(GetPatientAppointmentsQuery query, CancellationToken ct)
    {
        var list = await appointments.ListForPatientAsync(query.PatientId, ct);
        var clinicNames = new Dictionary<Guid, string>();
        var result = new List<AppointmentDto>();

        foreach (var appointment in list.OrderBy(a => a.StartsAt))
        {
            if (!clinicNames.TryGetValue(appointment.ClinicId, out var name))
                clinicNames[appointment.ClinicId] = name = (await mediator.Send(new GetClinicQuery(appointment.ClinicId), ct)).Name;
            result.Add(appointment.ToDto(name));
        }
        return result;
    }
}
