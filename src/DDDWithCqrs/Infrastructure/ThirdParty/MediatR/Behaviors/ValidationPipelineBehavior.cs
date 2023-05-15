using BLRefactoring.Shared.CQS;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR.Behaviors;

/// <summary>
/// Performs validation of MediatR's requests before it is handled by the handler.
/// </summary>
public class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        if (requestType.IsAssignableTo(typeof(ICommandBase)) && !_validators.Any())
        {
            throw new InvalidOperationException(
                $"Command {requestType.FullName} must have a validator even if it does nothing.");
        }

        var validationResults = new List<ValidationResult>();
        foreach (var validator in _validators)
        {
            validationResults.Add(await validator.ValidateAsync(request, cancellationToken));
        }

        var errors = validationResults.SelectMany(result => result.Errors);
        if (errors.Any())
        {
            throw new ValidationException(errors);
        }

        return await next();
    }
}
