using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Common.Results;

public interface IResult
{
    /// <summary>
    /// Gets a read-only collection of errors associated with the result.
    /// </summary>
    IReadOnlyErrorCollection Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the result is a success.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the result is a failure.
    /// </summary>
    bool IsFailure { get; }
}

/// <summary>
/// Provides a base class for <see cref="Result"/> and <see cref="Result{T}"/> classes.
/// </summary>
public abstract class ResultBase : ValueObject, IResult
{
    /// <summary>
    /// Gets a read-only collection of errors associated with the result.
    /// </summary>
    public IReadOnlyErrorCollection Errors { get; protected init; }

    /// <summary>
    /// Gets a value indicating whether the result is a success.
    /// </summary>
    public bool IsSuccess => !Errors.HasErrors();

    /// <summary>
    /// Gets a value indicating whether the result is a failure.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultBase"/> class with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to associate with the result. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    protected ResultBase(IReadOnlyErrorCollection errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }
}
