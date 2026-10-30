using System.Text;
using ClinicBookingSystem.Api.Endpoints;
using ClinicBookingSystem.Api.Identity;
using ClinicBookingSystem.Api.Infrastructure;
using ClinicBookingSystem.Appointments.Persistence;
using ClinicBookingSystem.Clinics.Persistence;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.Patients.Persistence;
using ClinicBookingSystem.TimeSlots.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/clinic-.log", rollingInterval: RollingInterval.Day));

    var connectionString = builder.Configuration.GetConnectionString("ClinicDb")
                           ?? throw new InvalidOperationException("Connection string 'ClinicDb' is missing.");

    // Modules: each owns its own DbContext and schema within the shared database.
    builder.Services
        .AddPatientsPersistence(connectionString)
        .AddClinicsPersistence(connectionString)
        .AddTimeSlotsPersistence(connectionString)
        .AddAppointmentsPersistence(connectionString);

    builder.Services.AddSingleton(TimeProvider.System);
    // The source generator scans every referenced module assembly for handlers.
    builder.Services.AddMediator(options =>
    {
        options.ServiceLifetime = ServiceLifetime.Scoped;
        options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
    });
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<ClinicBookingSystem.Appointments.Application.BookAppointmentCommandValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<ClinicBookingSystem.Clinics.Application.CreateClinicCommandValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<ClinicBookingSystem.Patients.Application.RegisterPatientCommandValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<ClinicBookingSystem.TimeSlots.Application.GenerateTimeSlotsCommandValidator>();

    // Identity + JWT
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters (use user-secrets or an environment variable).");

    builder.Services.AddDbContext<AppIdentityDbContext>(o => o.UseSqlServer(connectionString,
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AppIdentityDbContext.Schema)));
    builder.Services.AddIdentityCore<AppUser>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireUppercase = true;
            o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppIdentityDbContext>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        });
    builder.Services.AddAuthorization();

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader().AllowAnyMethod()));

    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // "Docker" is the local compose environment: same developer conveniences (Swagger, demo data) as Development.
    var isLocal = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker");
    if (isLocal)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapAuthEndpoints();
    app.MapClinicEndpoints();
    app.MapAppointmentEndpoints();

    await DatabaseInitializer.InitialiseAsync(app.Services, seed: isLocal);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposed so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
