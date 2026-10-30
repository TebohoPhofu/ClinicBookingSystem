using FluentValidation;
using Mediator;

namespace ClinicBookingSystem.Api.Infrastructure;

/// <summary>Runs every FluentValidation validator registered for a message before its handler executes.</summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count > 0)
        {
            var context = new ValidationContext<TMessage>(message);
            var failures = (await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0) throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
