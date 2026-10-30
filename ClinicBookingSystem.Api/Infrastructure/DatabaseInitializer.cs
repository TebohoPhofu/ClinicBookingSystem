using ClinicBookingSystem.Api.Identity;
using ClinicBookingSystem.Appointments.Persistence;
using ClinicBookingSystem.Clinics.Application;
using ClinicBookingSystem.Clinics.Persistence;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.Patients.Persistence;
using ClinicBookingSystem.TimeSlots.Application;
using ClinicBookingSystem.TimeSlots.Persistence;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicBookingSystem.Api.Infrastructure;

/// <summary>Applies each module's migrations and, in development, seeds demo data.</summary>
public static class DatabaseInitializer
{
    public static async Task InitialiseAsync(IServiceProvider services, bool seed)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<AppIdentityDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<PatientsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ClinicsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<TimeSlotsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AppointmentsDbContext>().Database.MigrateAsync();

        await SeedRolesAsync(sp);
        if (seed) await SeedDemoDataAsync(sp);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { Roles.Patient, Roles.ClinicStaff, Roles.Admin })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
    }

    private static async Task SeedDemoDataAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        const string adminEmail = "admin@phcis.local";
        if (await users.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser { Id = Guid.NewGuid(), UserName = adminEmail, Email = adminEmail, FullName = "System Admin", EmailConfirmed = true };
            await users.CreateAsync(admin, sp.GetRequiredService<IConfiguration>()["Seed:AdminPassword"] ?? "Admin#12345");
            await users.AddToRoleAsync(admin, Roles.Admin);
        }

        var mediator = sp.GetRequiredService<IMediator>();
        if ((await mediator.Send(new GetClinicsQuery())).Count > 0) return;

        var clock = sp.GetRequiredService<TimeProvider>();
        var today = DateOnly.FromDateTime(ClinicTime.ToClinicTime(clock.GetUtcNow()).DateTime);

        foreach (var (name, address, phone) in new[]
        {
            ("Soweto Community Clinic", "12 Vilakazi Street, Orlando West, Soweto", "011 555 0101"),
            ("Pretoria Central Clinic", "88 Church Street, Pretoria", "012 555 0102"),
            ("Khayelitsha Day Hospital", "5 Walter Sisulu Road, Khayelitsha", "021 555 0103")
        })
        {
            var clinic = await mediator.Send(new CreateClinicCommand(name, address, phone));
            for (var d = 0; d < 14; d++)
            {
                var date = today.AddDays(d);
                var (dayStart, _) = ClinicTime.DayRange(date);
                await mediator.Send(new GenerateTimeSlotsCommand(clinic.Id, dayStart.AddHours(8), dayStart.AddHours(16), 30));
            }
        }
    }
}
