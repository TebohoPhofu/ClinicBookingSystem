using ClinicBookingSystem.Clinics.Application;
using ClinicBookingSystem.Clinics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBookingSystem.Clinics.Persistence;

public sealed class ClinicsDbContext(DbContextOptions<ClinicsDbContext> options) : DbContext(options)
{
    public const string Schema = "clinics";
    public DbSet<Clinic> Clinics => Set<Clinic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Clinic>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Address).HasMaxLength(500).IsRequired();
            b.Property(c => c.PhoneNumber).HasMaxLength(20);
        });
    }
}

internal sealed class ClinicRepository(ClinicsDbContext db) : IClinicRepository
{
    public Task<Clinic?> GetByIdAsync(Guid id, CancellationToken ct) => db.Clinics.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Clinic>> ListActiveAsync(CancellationToken ct) =>
        await db.Clinics.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task AddAsync(Clinic clinic, CancellationToken ct) => await db.Clinics.AddAsync(clinic, ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public static class ClinicsPersistenceExtensions
{
    public static IServiceCollection AddClinicsPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClinicsDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ClinicsDbContext.Schema)));
        services.AddScoped<IClinicRepository, ClinicRepository>();
        return services;
    }
}
