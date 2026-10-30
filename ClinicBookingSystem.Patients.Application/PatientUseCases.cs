using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.Patients.Domain;
using ClinicBookingSystem.SharedKernel;
using FluentValidation;

namespace ClinicBookingSystem.Patients.Application;

public interface IPatientRepository
{
    Task<Patient?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct);
    Task AddAsync(Patient patient, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public static class PatientMapping
{
    public static PatientDto ToDto(this Patient p) =>
        new(p.Id, p.FirstName, p.LastName, p.Email, p.PhoneNumber, p.DateOfBirth);
}

public sealed record RegisterPatientCommand(
    Guid IdentityUserId, string FirstName, string LastName, string Email, string PhoneNumber, DateOnly DateOfBirth)
    : ICommand<PatientDto>;

public sealed class RegisterPatientCommandValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientCommandValidator()
    {
        RuleFor(x => x.IdentityUserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class RegisterPatientHandler(IPatientRepository patients, TimeProvider clock)
    : ICommandHandler<RegisterPatientCommand, PatientDto>
{
    public async ValueTask<PatientDto> Handle(RegisterPatientCommand command, CancellationToken ct)
    {
        if (await patients.GetByIdentityUserIdAsync(command.IdentityUserId, ct) is not null)
            throw new ConflictException("A patient profile already exists for this account.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var patient = Patient.Register(command.IdentityUserId, command.FirstName, command.LastName,
            command.Email, command.PhoneNumber, command.DateOfBirth, today);

        await patients.AddAsync(patient, ct);
        await patients.SaveChangesAsync(ct);
        return patient.ToDto();
    }
}

/// <summary>Resolves the patient profile belonging to a signed-in user.</summary>
public sealed record GetPatientByIdentityUserQuery(Guid IdentityUserId) : IQuery<PatientDto>;

public sealed class GetPatientByIdentityUserHandler(IPatientRepository patients)
    : IQueryHandler<GetPatientByIdentityUserQuery, PatientDto>
{
    public async ValueTask<PatientDto> Handle(GetPatientByIdentityUserQuery query, CancellationToken ct) =>
        (await patients.GetByIdentityUserIdAsync(query.IdentityUserId, ct))?.ToDto()
        ?? throw new NotFoundException("No patient profile exists for this account.");
}
