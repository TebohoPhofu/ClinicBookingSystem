using ClinicBookingSystem.SharedKernel;

namespace ClinicBookingSystem.TimeSlots.Domain;

/// <summary>Builds consecutive time slots for a clinic day.</summary>
public static class TimeSlotScheduler
{
    public static IReadOnlyList<TimeSlot> Generate(
        Guid clinicId, DateTimeOffset opensAt, DateTimeOffset closesAt, TimeSpan slotLength)
    {
        if (slotLength <= TimeSpan.Zero) throw new DomainException("Slot length must be positive.");
        if (closesAt <= opensAt) throw new DomainException("Closing time must be after opening time.");

        var slots = new List<TimeSlot>();
        for (var start = opensAt; start + slotLength <= closesAt; start += slotLength)
            slots.Add(TimeSlot.Create(clinicId, start, start + slotLength));
        return slots;
    }
}
