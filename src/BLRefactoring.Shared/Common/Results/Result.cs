
using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail, with a collection of errors that describe the failure.
/// </summary>
public sealed class Result : ResultBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to associate with the result. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    private Result(IReadOnlyErrorCollection errors) : base(errors)
    {
    }

    /// <summary>
    /// Combines the errors from the provided operation with the errors of the current result,
    /// creating a new <see cref="Result"/> containing the combined errors.
    /// </summary>
    /// <param name="operation">
    /// A function that performs an operation and returns a <see cref="Result"/>.
    /// Must not be null.
    /// </param>
    /// <returns>
    /// A new <see cref="Result"/> object containing the combined errors
    /// from the operation and the current result.
    /// </returns>
    public Result Combine(Func<Result> operation)
    {
        var operationResult = operation();
        var errors = new ErrorCollection(Errors);
        errors.AddErrors(operationResult.Errors);
        return new Result(errors);
    }

    /// <summary>
    /// Combines the result of an operation with the current result, creating a new result that represents the combined result.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="targetObject">The target object to combine with the current result.</param>
    /// <param name="operation">The operation to perform on the target object. Must not be null.</param>
    /// <returns>A new <see cref="Result"/> object that represents the combined result of the operation and the current result.</returns>
    public Result CombineWith<T>(T targetObject, Func<T, Result> operation)
    {
        var operationResult = operation(targetObject);
        var errors = new ErrorCollection(Errors);
        errors.AddErrors(operationResult.Errors);
        return new Result(errors);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a successful operation with no errors.
    /// </summary>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a successful operation with no errors.</returns>
    public static Result Success() => new(new ErrorCollection());

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed operation with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified</returns>
    public static Result Failure(ErrorCollection errors) => new(errors);

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed operation with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified</returns>
    public static Result Failure(IReadOnlyErrorCollection errors) => new(errors);

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed operation with the specified error.
    /// </summary>
    /// <param name="error">The error to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified error.</returns>
    public static Result Failure(Error error) => new(new ErrorCollection() { error });

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed operation with the specified error code and error message.
    /// </summary>
    /// <param name="errorCode">The error code to associate with the failed result. Must not be null.</param>
    /// <param name="errorMessage">The error message to associate with the failed result. Must not be null or empty.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified error code and error message.</returns>
    public static Result Failure(ErrorCode errorCode, string errorMessage) =>
        Failure(new ErrorCollection { new(errorCode, errorMessage) });

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed operation with the specified error message and an unspecified error code.
    /// </summary>
    /// <param name="errorMessage">The error message to associate with the failed result. Must not be null or empty.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified error message and an unspecified error code.</returns>
    public static Result Failure(string errorMessage) =>
        Failure(new ErrorCollection { new(ErrorCode.Unspecified, errorMessage) });

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return Errors.OrderBy(e => e.ErrorCode)
            .ThenBy(e => e.ErrorMessage)
            .ThenBy(e => e.OccurredOn);
    }

    /// <summary>
    /// Converts an error message into a new instance of the <see cref="Result"/> class that represents a failed operation with the specified error message and an unspecified error code.
    /// </summary>
    /// <param name="errorMessage">The error message to associate with the failed result. Must not be null or empty.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified error message and an unspecified error code.</returns>
    public static implicit operator Result(string errorMessage) => Failure(errorMessage);

    /// <summary>
    /// Converts an <see cref="ErrorCollection"/> object into a new instance of the <see cref="Result"/> class that represents a failed operation with the specified collection of errors.
    /// </summary>
    /// <param name="errorCollection">The collection of errors to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified collection of errors.</returns>
    public static implicit operator Result(ErrorCollection errorCollection) => Failure(errorCollection);

    /// <summary>
    /// Converts an <see cref="Error"/> object into a new instance of the <see cref="Result"/> class that represents a failed operation with the specified error.
    /// </summary>
    /// <param name="error">The error to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed operation with the specified error.</returns>
    public static implicit operator Result(Error error) => Failure(error);

    public void ThrowOnError()
    {
        if (IsFailure)
        {
            throw new DomainException(this);
        }
    }
}
