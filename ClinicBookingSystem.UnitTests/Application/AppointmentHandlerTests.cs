using ClinicBookingSystem.Appointments.Application;
using ClinicBookingSystem.Appointments.Domain;
using ClinicBookingSystem.Clinics.Application;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Application;
using Mediator;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace ClinicBookingSystem.UnitTests.Application;

public class AppointmentHandlerTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly IAppointmentRepository _appointments = Substitute.For<IAppointmentRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TimeProvider _clock = new FixedTimeProvider(Now);
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly ClinicDto _clinic = new(Guid.NewGuid(), "Soweto Community Clinic", "1 Main Rd", "0110000000");

    private TimeSlotDto Slot(int daysAhead = 1) =>
        new(Guid.NewGuid(), _clinic.Id, Now.AddDays(daysAhead), Now.AddDays(daysAhead).AddMinutes(30), true);

    private void GivenSlot(TimeSlotDto slot)
    {
        _mediator.Send(Arg.Is<GetTimeSlotQuery>(q => q.TimeSlotId == slot.Id), Arg.Any<CancellationToken>()).Returns(slot);
        _mediator.Send(Arg.Any<GetClinicQuery>(), Arg.Any<CancellationToken>()).Returns(_clinic);
    }

    private Appointment ExistingAppointment(TimeSlotDto slot) =>
        Appointment.Book(_patientId, slot.ClinicId, slot.Id, slot.StartsAt, slot.EndsAt, Now);

    // ---- Booking ----

    [Fact]
    public async Task Book_FreeSlot_ReservesSlotSavesAndReturnsConfirmation()
    {
        var slot = Slot();
        GivenSlot(slot);
        var sut = new BookAppointmentHandler(_appointments, _mediator, _clock);

        var result = await sut.Handle(new BookAppointmentCommand(_patientId, slot.Id), CancellationToken.None);

        result.Appointment.Status.ShouldBe("Confirmed");
        result.Appointment.ClinicName.ShouldBe(_clinic.Name);
        result.Message.ShouldContain(result.Appointment.ConfirmationNumber);
        result.Message.ShouldContain(_clinic.Name);
        await _mediator.Received(1).Send(Arg.Is<ReserveTimeSlotCommand>(c => c.TimeSlotId == slot.Id), Arg.Any<CancellationToken>());
        await _appointments.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _appointments.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Book_PatientAlreadyHasOverlappingAppointment_ThrowsConflictWithoutReserving()
    {
        var slot = Slot();
        GivenSlot(slot);
        _appointments.HasOverlappingAsync(_patientId, slot.StartsAt, slot.EndsAt, null, Arg.Any<CancellationToken>()).Returns(true);
        var sut = new BookAppointmentHandler(_appointments, _mediator, _clock);

        await Should.ThrowAsync<ConflictException>(() =>
            sut.Handle(new BookAppointmentCommand(_patientId, slot.Id), CancellationToken.None).AsTask());

        await _mediator.DidNotReceive().Send(Arg.Any<ReserveTimeSlotCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Book_SlotAlreadyTaken_ThrowsConflictAndSavesNothing()
    {
        var slot = Slot();
        GivenSlot(slot);
        _mediator.Send(Arg.Any<ReserveTimeSlotCommand>(), Arg.Any<CancellationToken>())
            .Throws(new ConflictException("This time slot has already been booked."));
        var sut = new BookAppointmentHandler(_appointments, _mediator, _clock);

        await Should.ThrowAsync<ConflictException>(() =>
            sut.Handle(new BookAppointmentCommand(_patientId, slot.Id), CancellationToken.None).AsTask());

        await _appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Book_SaveFails_ReleasesTheSlot()
    {
        var slot = Slot();
        GivenSlot(slot);
        _appointments.SaveChangesAsync(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("db down"));
        var sut = new BookAppointmentHandler(_appointments, _mediator, _clock);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.Handle(new BookAppointmentCommand(_patientId, slot.Id), CancellationToken.None).AsTask());

        await _mediator.Received(1).Send(Arg.Is<ReleaseTimeSlotCommand>(c => c.TimeSlotId == slot.Id), Arg.Any<CancellationToken>());
    }

    // ---- Cancellation ----

    [Fact]
    public async Task Cancel_OwnAppointment_CancelsAndReleasesSlot()
    {
        var slot = Slot();
        var appointment = ExistingAppointment(slot);
        _appointments.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        var sut = new CancelAppointmentHandler(_appointments, _mediator, _clock);

        await sut.Handle(new CancelAppointmentCommand(_patientId, appointment.Id), CancellationToken.None);

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        await _mediator.Received(1).Send(Arg.Is<ReleaseTimeSlotCommand>(c => c.TimeSlotId == slot.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_AnotherPatientsAppointment_ThrowsNotFound()
    {
        var appointment = ExistingAppointment(Slot());
        _appointments.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        var sut = new CancelAppointmentHandler(_appointments, _mediator, _clock);

        await Should.ThrowAsync<NotFoundException>(() =>
            sut.Handle(new CancelAppointmentCommand(Guid.NewGuid(), appointment.Id), CancellationToken.None).AsTask());

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    // ---- Rescheduling ----

    [Fact]
    public async Task Reschedule_ToFreeSlot_MovesAppointmentAndReleasesOldSlot()
    {
        var oldSlot = Slot(1);
        var newSlot = Slot(3);
        var appointment = ExistingAppointment(oldSlot);
        _appointments.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        GivenSlot(newSlot);
        var sut = new RescheduleAppointmentHandler(_appointments, _mediator, _clock);

        var result = await sut.Handle(new RescheduleAppointmentCommand(_patientId, appointment.Id, newSlot.Id), CancellationToken.None);

        appointment.TimeSlotId.ShouldBe(newSlot.Id);
        result.Message.ShouldContain("rescheduled");
        await _mediator.Received(1).Send(Arg.Is<ReserveTimeSlotCommand>(c => c.TimeSlotId == newSlot.Id), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<ReleaseTimeSlotCommand>(c => c.TimeSlotId == oldSlot.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reschedule_ToSameSlot_ThrowsDomainException()
    {
        var slot = Slot();
        var appointment = ExistingAppointment(slot);
        _appointments.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        var sut = new RescheduleAppointmentHandler(_appointments, _mediator, _clock);

        await Should.ThrowAsync<DomainException>(() =>
            sut.Handle(new RescheduleAppointmentCommand(_patientId, appointment.Id, slot.Id), CancellationToken.None).AsTask());
    }
}
