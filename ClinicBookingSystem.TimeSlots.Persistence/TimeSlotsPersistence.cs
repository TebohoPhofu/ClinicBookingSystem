using ClinicBookingSystem.SharedKernel;
using ClinicBookingSystem.TimeSlots.Application;
using ClinicBookingSystem.TimeSlots.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicBookingSystem.TimeSlots.Persistence;

public sealed class TimeSlotsDbContext(DbContextOptions<TimeSlotsDbContext> options) : DbContext(options)
{
    public const string Schema = "timeslots";
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TimeSlot>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.RowVersion).IsRowVersion();
            b.HasIndex(s => new { s.ClinicId, s.StartsAt }).IsUnique();
        });
    }
}

internal sealed class TimeSlotRepository(TimeSlotsDbContext db) : ITimeSlotRepository
{
    public Task<TimeSlot?> GetByIdAsync(Guid id, CancellationToken ct) => db.TimeSlots.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<TimeSlot>> ListAsync(Guid clinicId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.TimeSlots.Where(s => s.ClinicId == clinicId && s.StartsAt >= from && s.StartsAt < to).ToListAsync(ct);

    public Task AddRangeAsync(IEnumerable<TimeSlot> slots, CancellationToken ct) => db.TimeSlots.AddRangeAsync(slots, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("This time slot has just been booked by someone else.", ex);
        }
    }
}

public static class TimeSlotsPersistenceExtensions
{
    public static IServiceCollection AddTimeSlotsPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TimeSlotsDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TimeSlotsDbContext.Schema)));
        services.AddScoped<ITimeSlotRepository, TimeSlotRepository>();
        return services;
    }
}
