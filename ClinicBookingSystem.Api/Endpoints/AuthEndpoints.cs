using System.Security.Claims;
using ClinicBookingSystem.Api.Identity;
using ClinicBookingSystem.Contracts;
using ClinicBookingSystem.Patients.Application;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Identity;

namespace ClinicBookingSystem.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        group.MapPost("/register", Register).WithSummary("Create a patient account and sign in.");
        group.MapPost("/login", Login).WithSummary("Sign in and receive a JWT.");
        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, IValidator<RegisterRequest> validator, UserManager<AppUser> users,
        IMediator mediator, ITokenService tokens, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = $"{request.FirstName} {request.LastName}".Trim()
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Results.ValidationProblem(created.Errors.GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

        try
        {
            await users.AddToRoleAsync(user, Roles.Patient);
            await mediator.Send(new RegisterPatientCommand(user.Id, request.FirstName, request.LastName,
                request.Email, request.PhoneNumber, DateOnly.FromDateTime(request.DateOfBirth!.Value)), ct);
        }
        catch
        {
            // Do not leave a login without a patient profile behind.
            await users.DeleteAsync(user);
            throw;
        }

        return Results.Ok(tokens.CreateToken(user, [Roles.Patient]));
    }

    private static async Task<IResult> Login(
        LoginRequest request, IValidator<LoginRequest> validator, UserManager<AppUser> users, ITokenService tokens, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);

        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, request.Password))
        {
            if (user is not null) await users.AccessFailedAsync(user);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }

        await users.ResetAccessFailedCountAsync(user);
        return Results.Ok(tokens.CreateToken(user, (await users.GetRolesAsync(user)).ToList()));
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}
