using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Application;
using ClinicBookingSystem.TimeSlots.Domain;
using NSubstitute;
using Shouldly;

namespace ClinicBookingSystem.UnitTests.Application;

public class TimeSlotHandlerTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly ITimeSlotRepository _slots = Substitute.For<ITimeSlotRepository>();
    private readonly TimeProvider _clock = new FixedTimeProvider(Now);
    private readonly Guid _clinicId = Guid.NewGuid();

    [Fact]
    public async Task GetTimeSlots_OnlyAvailable_ExcludesReservedAndPastSlots()
    {
        var free = TimeSlot.Create(_clinicId, Now.AddHours(2), Now.AddHours(3));
        var reserved = TimeSlot.Create(_clinicId, Now.AddHours(4), Now.AddHours(5));
        reserved.Reserve(Now);
        var past = TimeSlot.Create(_clinicId, Now.AddHours(-2), Now.AddHours(-1));
        _slots.ListAsync(_clinicId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([reserved, past, free]);
        var sut = new GetTimeSlotsHandler(_slots, _clock);

        var result = await sut.Handle(new GetTimeSlotsQuery(_clinicId, Now.AddDays(-1), Now.AddDays(1)), CancellationToken.None);

        result.Select(s => s.Id).ShouldBe([free.Id]);
    }

    [Fact]
    public async Task Reserve_MissingSlot_ThrowsNotFound()
    {
        var sut = new ReserveTimeSlotHandler(_slots, _clock);

        await Should.ThrowAsync<NotFoundException>(() =>
            sut.Handle(new ReserveTimeSlotCommand(Guid.NewGuid()), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Reserve_AlreadyReserved_ThrowsConflict()
    {
        var slot = TimeSlot.Create(_clinicId, Now.AddHours(2), Now.AddHours(3));
        slot.Reserve(Now);
        _slots.GetByIdAsync(slot.Id, Arg.Any<CancellationToken>()).Returns(slot);
        var sut = new ReserveTimeSlotHandler(_slots, _clock);

        await Should.ThrowAsync<ConflictException>(() =>
            sut.Handle(new ReserveTimeSlotCommand(slot.Id), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Generate_SkipsSlotsThatAlreadyExist()
    {
        var existing = TimeSlot.Create(_clinicId, Now, Now.AddMinutes(30));
        _slots.ListAsync(_clinicId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([existing]);
        var sut = new GenerateTimeSlotsHandler(_slots);

        var created = await sut.Handle(new GenerateTimeSlotsCommand(_clinicId, Now, Now.AddHours(2), 30), CancellationToken.None);

        created.ShouldBe(3);
    }
}
