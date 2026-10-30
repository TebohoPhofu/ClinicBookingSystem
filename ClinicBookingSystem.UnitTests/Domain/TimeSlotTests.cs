using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Domain;
using Shouldly;

namespace ClinicBookingSystem.UnitTests.Domain;

public class TimeSlotTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static TimeSlot FutureSlot() => TimeSlot.Create(Guid.NewGuid(), Now.AddDays(1), Now.AddDays(1).AddMinutes(30));

    [Fact]
    public void Reserve_FreeSlot_MarksSlotReserved()
    {
        var slot = FutureSlot();

        slot.Reserve(Now);

        slot.IsReserved.ShouldBeTrue();
    }

    [Fact]
    public void Reserve_AlreadyReservedSlot_ThrowsConflict()
    {
        var slot = FutureSlot();
        slot.Reserve(Now);

        Should.Throw<ConflictException>(() => slot.Reserve(Now));
    }

    [Fact]
    public void Reserve_SlotInThePast_ThrowsDomainException()
    {
        var slot = FutureSlot();

        Should.Throw<DomainException>(() => slot.Reserve(Now.AddDays(2)));
    }

    [Fact]
    public void Release_ReservedSlot_MakesItAvailableAgain()
    {
        var slot = FutureSlot();
        slot.Reserve(Now);

        slot.Release();

        slot.IsReserved.ShouldBeFalse();
    }

    [Fact]
    public void Create_EndBeforeStart_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() => TimeSlot.Create(Guid.NewGuid(), Now, Now.AddMinutes(-5)));
    }

    [Fact]
    public void Generate_EightHourDayInHalfHourSlots_Creates16ContiguousSlots()
    {
        var slots = TimeSlotScheduler.Generate(Guid.NewGuid(), Now, Now.AddHours(8), TimeSpan.FromMinutes(30));

        slots.Count.ShouldBe(16);
        slots[0].StartsAt.ShouldBe(Now);
        slots[^1].EndsAt.ShouldBe(Now.AddHours(8));
        for (var i = 1; i < slots.Count; i++)
            slots[i].StartsAt.ShouldBe(slots[i - 1].EndsAt);
    }

    [Fact]
    public void Generate_TrailingPartialSlot_IsDropped()
    {
        var slots = TimeSlotScheduler.Generate(Guid.NewGuid(), Now, Now.AddMinutes(50), TimeSpan.FromMinutes(30));

        slots.Count.ShouldBe(1);
    }
}
