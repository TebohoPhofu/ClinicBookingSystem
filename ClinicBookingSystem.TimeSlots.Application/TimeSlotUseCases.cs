using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Domain;
using FluentValidation;

namespace ClinicBookingSystem.TimeSlots.Application;

public interface ITimeSlotRepository
{
    Task<TimeSlot?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TimeSlot>> ListAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<TimeSlot> slots, CancellationToken ct);

    /// <exception cref="ConflictException">Another request modified the same slot concurrently.</exception>
    Task SaveChangesAsync(CancellationToken ct);
}

public static class TimeSlotMapping
{
    public static TimeSlotDto ToDto(this TimeSlot s) => new(s.Id, s.ClinicId, s.StartsAt, s.EndsAt, !s.IsReserved);
}

/// <summary>Lists the slots of one clinic starting within [From, To); only free, future slots unless asked otherwise.</summary>
public sealed record GetTimeSlotsQuery(Guid ClinicId, DateTimeOffset From, DateTimeOffset To, bool OnlyAvailable = true)
    : IQuery<IReadOnlyList<TimeSlotDto>>;

public sealed class GetTimeSlotsHandler(ITimeSlotRepository slots, TimeProvider clock)
    : IQueryHandler<GetTimeSlotsQuery, IReadOnlyList<TimeSlotDto>>
{
    public async ValueTask<IReadOnlyList<TimeSlotDto>> Handle(GetTimeSlotsQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var found = await slots.ListAsync(query.ClinicId, query.From, query.To, ct);
        return found
            .Where(s => !query.OnlyAvailable || (!s.IsReserved && s.StartsAt > now))
            .OrderBy(s => s.StartsAt)
            .Select(s => s.ToDto())
            .ToList();
    }
}

public sealed record GetTimeSlotQuery(Guid TimeSlotId) : IQuery<TimeSlotDto>;

public sealed class GetTimeSlotHandler(ITimeSlotRepository slots) : IQueryHandler<GetTimeSlotQuery, TimeSlotDto>
{
    public async ValueTask<TimeSlotDto> Handle(GetTimeSlotQuery query, CancellationToken ct) =>
        (await slots.GetByIdAsync(query.TimeSlotId, ct))?.ToDto()
        ?? throw new NotFoundException("Time slot not found.");
}

/// <summary>Cross-module command used by Appointments to claim a slot atomically.</summary>
public sealed record ReserveTimeSlotCommand(Guid TimeSlotId) : ICommand<TimeSlotDto>;

public sealed class ReserveTimeSlotHandler(ITimeSlotRepository slots, TimeProvider clock)
    : ICommandHandler<ReserveTimeSlotCommand, TimeSlotDto>
{
    public async ValueTask<TimeSlotDto> Handle(ReserveTimeSlotCommand command, CancellationToken ct)
    {
        var slot = await slots.GetByIdAsync(command.TimeSlotId, ct)
                   ?? throw new NotFoundException("Time slot not found.");

        slot.Reserve(clock.GetUtcNow());
        await slots.SaveChangesAsync(ct);
        return slot.ToDto();
    }
}

public sealed record ReleaseTimeSlotCommand(Guid TimeSlotId) : ICommand;

public sealed class ReleaseTimeSlotHandler(ITimeSlotRepository slots) : ICommandHandler<ReleaseTimeSlotCommand>
{
    public async ValueTask<Unit> Handle(ReleaseTimeSlotCommand command, CancellationToken ct)
    {
        var slot = await slots.GetByIdAsync(command.TimeSlotId, ct)
                   ?? throw new NotFoundException("Time slot not found.");

        slot.Release();
        await slots.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Opens a day of slots; slots that already exist at the same start are skipped so the command is repeatable.</summary>
public sealed record GenerateTimeSlotsCommand(Guid ClinicId, DateTimeOffset OpensAt, DateTimeOffset ClosesAt, int SlotMinutes)
    : ICommand<int>;

public sealed class GenerateTimeSlotsCommandValidator : AbstractValidator<GenerateTimeSlotsCommand>
{
    public GenerateTimeSlotsCommandValidator()
    {
        RuleFor(x => x.ClinicId).NotEmpty();
        RuleFor(x => x.SlotMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.ClosesAt).GreaterThan(x => x.OpensAt);
    }
}

public sealed class GenerateTimeSlotsHandler(ITimeSlotRepository slots) : ICommandHandler<GenerateTimeSlotsCommand, int>
{
    public async ValueTask<int> Handle(GenerateTimeSlotsCommand command, CancellationToken ct)
    {
        var generated = TimeSlotScheduler.Generate(
            command.ClinicId, command.OpensAt, command.ClosesAt, TimeSpan.FromMinutes(command.SlotMinutes));

        var existing = (await slots.ListAsync(command.ClinicId, command.OpensAt, command.ClosesAt, ct))
            .Select(s => s.StartsAt).ToHashSet();
        var toAdd = generated.Where(s => !existing.Contains(s.StartsAt)).ToList();

        await slots.AddRangeAsync(toAdd, ct);
        await slots.SaveChangesAsync(ct);
        return toAdd.Count;
    }
}
