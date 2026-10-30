using ClinicBookingSystem.Clinics.Domain;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.SharedKernel;
using FluentValidation;

namespace ClinicBookingSystem.Clinics.Application;

public interface IClinicRepository
{
    Task<Clinic?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Clinic>> ListActiveAsync(CancellationToken ct);
    Task AddAsync(Clinic clinic, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public static class ClinicMapping
{
    public static ClinicDto ToDto(this Clinic c) => new(c.Id, c.Name, c.Address, c.PhoneNumber);
}

public sealed record GetClinicsQuery : IQuery<IReadOnlyList<ClinicDto>>;

public sealed class GetClinicsHandler(IClinicRepository clinics) : IQueryHandler<GetClinicsQuery, IReadOnlyList<ClinicDto>>
{
    public async ValueTask<IReadOnlyList<ClinicDto>> Handle(GetClinicsQuery query, CancellationToken ct) =>
        (await clinics.ListActiveAsync(ct)).Select(c => c.ToDto()).ToList();
}

public sealed record GetClinicQuery(Guid ClinicId) : IQuery<ClinicDto>;

public sealed class GetClinicHandler(IClinicRepository clinics) : IQueryHandler<GetClinicQuery, ClinicDto>
{
    public async ValueTask<ClinicDto> Handle(GetClinicQuery query, CancellationToken ct) =>
        (await clinics.GetByIdAsync(query.ClinicId, ct))?.ToDto()
        ?? throw new NotFoundException("Clinic not found.");
}

public sealed record CreateClinicCommand(string Name, string Address, string PhoneNumber) : ICommand<ClinicDto>;

public sealed class CreateClinicCommandValidator : AbstractValidator<CreateClinicCommand>
{
    public CreateClinicCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateClinicHandler(IClinicRepository clinics) : ICommandHandler<CreateClinicCommand, ClinicDto>
{
    public async ValueTask<ClinicDto> Handle(CreateClinicCommand command, CancellationToken ct)
    {
        var clinic = Clinic.Create(command.Name, command.Address, command.PhoneNumber);
        await clinics.AddAsync(clinic, ct);
        await clinics.SaveChangesAsync(ct);
        return clinic.ToDto();
    }
}
