using ClinicBookingSystem.Appointments.Domain;
using ClinicBookingSystem.SharedKernel;
using Shouldly;

namespace ClinicBookingSystem.UnitTests.Domain;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static Appointment BookTomorrow() =>
        Appointment.Book(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(1), Now.AddDays(1).AddMinutes(30), Now);

    [Fact]
    public void Book_ValidInput_IsConfirmedWithConfirmationNumber()
    {
        var appointment = BookTomorrow();

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.ConfirmationNumber.ShouldStartWith("APT-");
        appointment.BookedAt.ShouldBe(Now);
    }

    [Fact]
    public void Book_InThePast_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
            Appointment.Book(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(-1), Now, Now));
    }

    [Fact]
    public void Cancel_ConfirmedAppointment_MarksCancelled()
    {
        var appointment = BookTomorrow();

        appointment.Cancel(Now);

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        appointment.CancelledAt.ShouldBe(Now);
    }

    [Fact]
    public void Cancel_TwiceThrowsDomainException()
    {
        var appointment = BookTomorrow();
        appointment.Cancel(Now);

        Should.Throw<DomainException>(() => appointment.Cancel(Now));
    }

    [Fact]
    public void Cancel_AfterStart_ThrowsDomainException()
    {
        var appointment = BookTomorrow();

        Should.Throw<DomainException>(() => appointment.Cancel(Now.AddDays(2)));
    }

    [Fact]
    public void Reschedule_ConfirmedAppointment_MovesToNewSlotAndKeepsConfirmationNumber()
    {
        var appointment = BookTomorrow();
        var number = appointment.ConfirmationNumber;
        var newSlot = Guid.NewGuid();

        appointment.Reschedule(newSlot, appointment.ClinicId, Now.AddDays(3), Now.AddDays(3).AddMinutes(30), Now);

        appointment.TimeSlotId.ShouldBe(newSlot);
        appointment.ConfirmationNumber.ShouldBe(number);
    }

    [Fact]
    public void Reschedule_CancelledAppointment_ThrowsDomainException()
    {
        var appointment = BookTomorrow();
        appointment.Cancel(Now);

        Should.Throw<DomainException>(() =>
            appointment.Reschedule(Guid.NewGuid(), appointment.ClinicId, Now.AddDays(3), Now.AddDays(3).AddMinutes(30), Now));
    }
}
