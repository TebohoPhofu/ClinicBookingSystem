using ClinicBookingSystem.Appointments.Application;
using ClinicBookingSystem.Appointments.Domain;
using ClinicBookingSystem.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBookingSystem.Appointments.Persistence;

public sealed class AppointmentsDbContext(DbContextOptions<AppointmentsDbContext> options) : DbContext(options)
{
    public const string Schema = "appointments";
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Appointment>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(a => a.ConfirmationNumber).HasMaxLength(20).IsRequired();
            b.HasIndex(a => a.ConfirmationNumber).IsUnique();
            b.HasIndex(a => a.PatientId);

            // Database-level guarantee against double booking: one confirmed appointment per slot.
            b.HasIndex(a => a.TimeSlotId).IsUnique().HasFilter("[Status] = 'Confirmed'");
        });
    }
}

internal sealed class AppointmentRepository(AppointmentsDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) => db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListForPatientAsync(Guid patientId, CancellationToken ct) =>
        await db.Appointments.Where(a => a.PatientId == patientId).ToListAsync(ct);

    public Task<bool> HasOverlappingAsync(Guid patientId, DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? excludeAppointmentId, CancellationToken ct) =>
        db.Appointments.AnyAsync(a => a.PatientId == patientId
                                      && a.Status == AppointmentStatus.Confirmed
                                      && a.Id != excludeAppointmentId
                                      && a.StartsAt < endsAt && a.EndsAt > startsAt, ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct) => await db.Appointments.AddAsync(appointment, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new ConflictException("This time slot has already been booked.", ex);
        }
    }
}

public static class AppointmentsPersistenceExtensions
{
    public static IServiceCollection AddAppointmentsPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppointmentsDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AppointmentsDbContext.Schema)));
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        return services;
    }
}
