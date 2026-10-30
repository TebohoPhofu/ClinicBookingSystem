namespace ClinicBookingSystem.SharedKernel;

/// <summary>Base type for all domain entities, identified by a <see cref="Guid"/>.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
