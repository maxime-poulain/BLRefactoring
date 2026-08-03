using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;

/// <summary>
/// Performs validation of Mediator's requests before it is handled by the handler.
/// </summary>
/// <remarks>
/// A command answers a rejection the way it answers every other failure: with a failed
/// <see cref="Result"/> carrying this API's error codes. It used to throw a
/// <see cref="ValidationException"/>, which <c>ValidationExceptionHandler</c> turned into a
/// <c>ValidationProblemDetails</c> — a second error shape, published under <c>errors</c>, for
/// failures the layered host reported under <c>domainErrors</c>. The same request was answered
/// differently depending on which host received it and, worse, on which rule it broke: a malformed
/// email left through the exception, while a bio of six hundred characters passed the validator,
/// reached the domain, and left as a <see cref="Result"/>.
/// <para>
/// Returning rather than throwing also stops using an exception for an outcome every caller is
/// expected to produce. Nothing is given up by it: the behaviour still returns before <c>next</c>
/// runs, so the handler is not entered and no aggregate is touched.
/// </para>
/// </remarks>
public sealed class ValidationPipelineBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = message.GetType();
        if (requestType.IsAssignableTo(typeof(ICommandBase)) && !validators.Any())
        {
            throw new InvalidOperationException(
                $"Command {requestType.FullName} must have a validator even if it does nothing.");
        }

        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(message, cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // Every command answers exactly Result, and the type system says so rather than the current
        // set of commands happening to agree: ICommand<TResult> is constrained to
        // `where TResult : Result`, and Result<TValue> is a separate class that does not derive from
        // Result — so ICommand<Result<T>> does not compile. There is one closed type to build here,
        // which is what spares this method the reflection that building "a failure of whatever
        // TResponse is" would otherwise need.
        if (typeof(TResponse) == typeof(Result))
        {
            var errors = new ErrorCollection(
                failures.Select(failure => new Error(ErrorCodes.Validation, failure.ErrorMessage)));

            return (TResponse)(object)Result.Failure(errors);
        }

        // A query answers with what it read — a DTO, a page — and has no failed state to return, so
        // its rejections still leave as an exception for ValidationExceptionHandler to answer. The
        // query validators of this stack guard one thing: an empty identifier reaching
        // EntityId.Create, which throws. This keeps that a 400 rather than the 500 it would
        // otherwise be. Giving queries the same treatment as commands means giving them a Result to
        // fail into, which is a change to the read side rather than to this behaviour.
        throw new ValidationException(failures);
    }
}
