using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;

/// <summary>
/// Represents a result of an operation that can either be successful with a value or a failure with a collection of <see cref="Error"/> objects.
/// </summary>
/// <typeparam name="T">The type of the value associated with a successful result.</typeparam>
public sealed class Result<T> : ResultBase
{
    private readonly T _value;

    /// <summary>
    /// Gets the value associated with a successful result. Throws an <see cref="InvalidOperationException"/> if the result is in an error state.
    /// </summary>
    public T Value
    {
        get
        {
            if (Errors.HasErrors())
            {
                throw new InvalidOperationException(
                    "Cannot access the Value property when the result is in an error state.");
            }

            return _value!;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class with the specified value and collection of errors.
    /// </summary>
    /// <param name="value">The value to associate with a successful result.</param>
    /// <param name="errors">The collection of errors to associate with a failed result. Must not be null.</param>
    /// <exception cref="InvalidOperationException">Thrown when the <paramref name="value"/> is null and the <paramref name="errors"/> collection is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the <paramref name="value"/> is not null and the <paramref name="errors"/> collection is not empty.</exception>
    private Result(T? value, IReadOnlyErrorCollection errors) : base(errors)
    {
        if (value == null && !errors.HasErrors())
        {
            throw new InvalidOperationException(
                "Result cannot be empty. It must either have a value or errors.");
        }

        if (value != null && errors.HasErrors())
        {
            throw new InvalidOperationException(
                "Result cannot have both a value and errors. It must either have a value or errors.");
        }

        _value = value!;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result{T}"/> class that represents a successful operation with the specified value.
    /// </summary>
    /// <param name="value">The value to associate with the successful result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a successful operation with the specified value.</returns>
    public static Result<T> Success(T value) => new(value, new ErrorCollection());

    /// <summary>
    /// Creates a new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified collection of errors.</returns>
    public static Result<T> Failure(IEnumerable<Error> errors) => new(default, new ErrorCollection(errors));

    /// <summary>
    /// Creates a new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error.
    /// </summary>
    /// <param name="error">The error to associate with the failed result. Must not be null.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error.</returns>
    public static Result<T> Failure(Error error) => new(default, new ErrorCollection() { error });

    /// <summary>
    /// Creates a new instance of the <see cref="Result{T}"/> class that represents a failed operation
    /// with the specified error message and an unspecified error code.
    /// </summary>
    /// <param name="errorMessage">The error message to associate with the failed result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error message.</returns>
    public static Result<T> Failure(string errorMessage) =>
        Failure(new ErrorCollection { new(ErrorCode.Unspecified, errorMessage) });

    /// <summary>
    /// Creates a new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error code and error message.
    /// </summary>
    /// <param name="errorCode">The error code to associate with the failed result.</param>
    /// <param name="errorMessage">The error message to associate with the failed result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error code and error message.</returns>
    public static Result<T> Failure(ErrorCode errorCode, string errorMessage) =>
        Failure(new ErrorCollection { new(errorCode, errorMessage) });

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        if (IsSuccess)
        {
            return new object[] { Value! };
        }

        return Errors.OrderBy(e => e.ErrorCode)
            .ThenBy(e => e.ErrorMessage)
            .ThenBy(e => e.OccurredOn);
    }

    /// <summary>
    /// Implicitly converts a string error message to a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="errorMessage">The error message to associate with the failed result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error message.</returns>
    public static implicit operator Result<T>(string errorMessage) => Failure(errorMessage);

    /// <summary>
    /// Implicitly converts an <see cref="ErrorCollection"/> to a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="errorCollection">The collection of errors to associate with the failed result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified collection of errors.</returns>
    public static implicit operator Result<T>(ErrorCollection errorCollection) => Failure(errorCollection);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="error">The error to associate with the failed result.</param>
    /// <returns>A new instance of the <see cref="Result{T}"/> class that represents a failed operation with the specified error.</returns>
    public static implicit operator Result<T>(Error error) => Failure(error);

    public void ThrowOnError()
    {
        if (IsFailure)
        {
            throw new DomainException(this);
        }
    }
}
