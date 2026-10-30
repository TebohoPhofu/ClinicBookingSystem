using ClinicBookingSystem.Patients.Application;
using ClinicBookingSystem.Patients.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBookingSystem.Patients.Persistence;

public sealed class PatientsDbContext(DbContextOptions<PatientsDbContext> options) : DbContext(options)
{
    public const string Schema = "patients";
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Patient>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            b.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            b.Property(p => p.Email).HasMaxLength(256).IsRequired();
            b.Property(p => p.PhoneNumber).HasMaxLength(20);
            b.Ignore(p => p.FullName);
            b.HasIndex(p => p.IdentityUserId).IsUnique();
        });
    }
}

internal sealed class PatientRepository(PatientsDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct) =>
        db.Patients.FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId, ct);

    public async Task AddAsync(Patient patient, CancellationToken ct) => await db.Patients.AddAsync(patient, ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public static class PatientsPersistenceExtensions
{
    public static IServiceCollection AddPatientsPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PatientsDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", PatientsDbContext.Schema)));
        services.AddScoped<IPatientRepository, PatientRepository>();
        return services;
    }
}
