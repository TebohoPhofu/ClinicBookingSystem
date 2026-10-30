using ClinicBookingSystem.SharedKernel;

namespace ClinicBookingSystem.Clinics.Domain;

/// <summary>A physical clinic where appointments take place.</summary>
public sealed class Clinic : Entity
{
    private Clinic() { }

    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static Clinic Create(string name, string address, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Clinic name is required.");
        if (string.IsNullOrWhiteSpace(address)) throw new DomainException("Clinic address is required.");

        return new Clinic { Name = name.Trim(), Address = address.Trim(), PhoneNumber = phoneNumber?.Trim() ?? string.Empty };
    }

    public void Deactivate() => IsActive = false;
}
