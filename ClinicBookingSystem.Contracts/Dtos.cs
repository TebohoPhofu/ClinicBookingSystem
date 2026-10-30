using FluentValidation;

namespace ClinicBookingSystem.Contracts;

public record ClinicDto(Guid Id, string Name, string Address, string PhoneNumber);

public record CreateClinicRequest(string Name, string Address, string PhoneNumber);

public record PatientDto(Guid Id, string FirstName, string LastName, string Email, string PhoneNumber, DateOnly DateOfBirth);

public record TimeSlotDto(Guid Id, Guid ClinicId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAvailable);

/// <summary>Admin request to open a day of bookable slots.</summary>
public record GenerateTimeSlotsRequest(Guid ClinicId, DateTimeOffset OpensAt, DateTimeOffset ClosesAt, int SlotMinutes);

public record GenerateTimeSlotsResponse(int Created);

public record BookAppointmentRequest(Guid TimeSlotId);

public record RescheduleAppointmentRequest(Guid NewTimeSlotId);

public record AppointmentDto(
    Guid Id, Guid ClinicId, string ClinicName, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Status, string ConfirmationNumber);

/// <summary>Returned after booking or rescheduling, including the human-readable confirmation message.</summary>
public record AppointmentConfirmationDto(AppointmentDto Appointment, string Message);

/// <summary>Mutable so it can be bound directly to a MudBlazor form.</summary>
public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, string Email, string FullName, IReadOnlyList<string> Roles);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+?[0-9 ]{9,15}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.DateOfBirth).NotNull().WithMessage("Date of birth is required.")
            .Must(d => d is null || d.Value.Date < DateTime.Today).WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password needs an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password needs a digit.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public static class Roles
{
    public const string Patient = "Patient";
    public const string ClinicStaff = "ClinicStaff";
    public const string Admin = "Admin";
}
