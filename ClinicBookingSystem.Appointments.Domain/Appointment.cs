using ClinicBookingSystem.SharedKernel;

namespace ClinicBookingSystem.Appointments.Domain;

/// <summary>A patient's confirmed (or cancelled) booking of a single time slot.</summary>
public sealed class Appointment : Entity
{
    private Appointment() { }

    public Guid PatientId { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid TimeSlotId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string ConfirmationNumber { get; private set; } = string.Empty;
    public DateTimeOffset BookedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public static Appointment Book(
        Guid patientId, Guid clinicId, Guid timeSlotId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        if (patientId == Guid.Empty) throw new DomainException("A patient is required.");
        if (clinicId == Guid.Empty) throw new DomainException("A clinic is required.");
        if (timeSlotId == Guid.Empty) throw new DomainException("A time slot is required.");
        if (startsAt <= now) throw new DomainException("Appointments cannot be booked in the past.");

        var id = Guid.NewGuid();
        return new Appointment
        {
            Id = id,
            PatientId = patientId,
            ClinicId = clinicId,
            TimeSlotId = timeSlotId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Confirmed,
            ConfirmationNumber = $"APT-{id.ToString("N")[..8].ToUpperInvariant()}",
            BookedAt = now
        };
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Cancelled) throw new DomainException("This appointment is already cancelled.");
        if (StartsAt <= now) throw new DomainException("An appointment that has started cannot be cancelled.");

        Status = AppointmentStatus.Cancelled;
        CancelledAt = now;
    }

    /// <summary>Moves a confirmed appointment to a different slot, keeping its confirmation number.</summary>
    public void Reschedule(Guid newTimeSlotId, Guid newClinicId, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Cancelled) throw new DomainException("A cancelled appointment cannot be rescheduled.");
        if (StartsAt <= now) throw new DomainException("An appointment that has started cannot be rescheduled.");
        if (startsAt <= now) throw new DomainException("Appointments cannot be rescheduled into the past.");

        TimeSlotId = newTimeSlotId;
        ClinicId = newClinicId;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }
}
