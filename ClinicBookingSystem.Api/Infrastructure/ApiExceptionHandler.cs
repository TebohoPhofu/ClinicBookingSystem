using ClinicBookingSystem.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBookingSystem.Api.Infrastructure;

/// <summary>Translates application exceptions into RFC 7807 problem responses.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException v => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct()),
                Extensions = { ["errors"] = v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()) }
            },
            NotFoundException n => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not found", Detail = n.Message },
            ConflictException c => new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = c.Message },
            DomainException d => new ProblemDetails { Status = StatusCodes.Status422UnprocessableEntity, Title = "Business rule violated", Detail = d.Message },
            _ => null
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception");
            problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "An unexpected error occurred" };
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
