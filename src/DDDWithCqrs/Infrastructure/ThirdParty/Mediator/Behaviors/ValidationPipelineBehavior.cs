using BLRefactoring.Shared.CQS;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;

/// <summary>
/// Performs validation of Mediator's requests before it is handled by the handler.
/// </summary>
public class ValidationPipelineBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = message.GetType();
        if (requestType.IsAssignableTo(typeof(ICommandBase)) && !validators.Any())
        {
            throw new InvalidOperationException(
                $"Command {requestType.FullName} must have a validator even if it does nothing.");
        }

        var validationResults = new List<ValidationResult>();
        foreach (var validator in validators)
        {
            validationResults.Add(await validator.ValidateAsync(message, cancellationToken));
        }

        var errors = validationResults.SelectMany(result => result.Errors).ToArray();
        if (errors.Any())
        {
            throw new ValidationException(errors);
        }

        return await next(message, cancellationToken);
    }
}
