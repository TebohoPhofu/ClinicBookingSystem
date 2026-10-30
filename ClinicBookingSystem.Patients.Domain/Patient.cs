using ClinicBookingSystem.SharedKernel;

namespace ClinicBookingSystem.Patients.Domain;

/// <summary>A person who books appointments. Linked to a login via <see cref="IdentityUserId"/>.</summary>
public sealed class Patient : Entity
{
    private Patient() { }

    public Guid IdentityUserId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public static Patient Register(
        Guid identityUserId, string firstName, string lastName, string email,
        string phoneNumber, DateOnly dateOfBirth, DateOnly today)
    {
        if (identityUserId == Guid.Empty) throw new DomainException("A patient must be linked to a user account.");
        if (string.IsNullOrWhiteSpace(firstName)) throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new DomainException("Last name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new DomainException("A valid email is required.");
        if (dateOfBirth >= today) throw new DomainException("Date of birth must be in the past.");

        return new Patient
        {
            IdentityUserId = identityUserId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber?.Trim() ?? string.Empty,
            DateOfBirth = dateOfBirth
        };
    }
}
