using ClinicBookingSystem.SharedKernel;

namespace ClinicBookingSystem.TimeSlots.Domain;

/// <summary>
/// A bookable window at a clinic. A slot can be reserved by at most one appointment;
/// this invariant is the core of double-booking prevention.
/// </summary>
public sealed class TimeSlot : Entity
{
    private TimeSlot() { }

    public Guid ClinicId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsReserved { get; private set; }

    /// <summary>Optimistic concurrency token so two simultaneous reservations cannot both succeed.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static TimeSlot Create(Guid clinicId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (clinicId == Guid.Empty) throw new DomainException("A time slot must belong to a clinic.");
        if (endsAt <= startsAt) throw new DomainException("A time slot must end after it starts.");

        return new TimeSlot { ClinicId = clinicId, StartsAt = startsAt, EndsAt = endsAt };
    }

    /// <summary>Reserves the slot for an appointment.</summary>
    /// <exception cref="ConflictException">The slot is already reserved.</exception>
    /// <exception cref="DomainException">The slot has already started.</exception>
    public void Reserve(DateTimeOffset now)
    {
        if (StartsAt <= now) throw new DomainException("This time slot is in the past and can no longer be booked.");
        if (IsReserved) throw new ConflictException("This time slot has already been booked.");
        IsReserved = true;
    }

    /// <summary>Frees the slot, e.g. after a cancellation or reschedule. Idempotent.</summary>
    public void Release() => IsReserved = false;
}
