using System.Collections;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// A mutable collection of <see cref="Error"/>, used to accumulate everything that went wrong in
/// one pass instead of stopping at the first problem.
/// </summary>
/// <remarks>
/// Consumers only ever see it as <see cref="IReadOnlyErrorCollection"/>. It used to implement a
/// mutable <c>IErrorCollection</c> interface as well, alongside an <c>AsReadOnly()</c> that
/// returned <c>this</c> — a read-only view any caller could cast straight back to a mutable one.
/// </remarks>
public sealed class ErrorCollection : IReadOnlyErrorCollection
{
    private readonly List<Error> _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCollection"/> class.
    /// </summary>
    public ErrorCollection()
    {
        _errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCollection"/> class with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to add to the new error collection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public ErrorCollection(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors = new List<Error>(errors);
    }

    /// <summary>
    /// Adds an <see cref="Error"/> to the collection.
    /// </summary>
    /// <param name="error">The error to add. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public void Add(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _errors.Add(error);
    }

    /// <summary>
    /// Adds an error to the collection from an error code and a message.
    /// </summary>
    public void Add(ErrorCode errorCode, string errorMessage)
    {
        Add(new Error(errorCode, errorMessage));
    }

    /// <summary>
    /// Adds a collection of <see cref="Error"/> to the collection.
    /// </summary>
    /// <param name="errors">The errors to add. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public void AddErrors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors.AddRange(errors);
    }

    /// <summary>
    /// Adds the errors carried by a failed <see cref="Result"/>, and nothing for a successful one.
    /// </summary>
    public void AddErrors(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Match(() => this, errors =>
        {
            AddErrors(errors);
            return this;
        });
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="IEnumerator{T}"/> objects in this error collection.
    /// </summary>
    /// <returns>An <see cref="Error"/> object that can be used to iterate through the <see cref="Error"/> objects in this error collection.</returns>
    public IEnumerator<Error> GetEnumerator()
    {
        return _errors.GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Turns this collection into a <see cref="Result"/>.
    /// </summary>
    /// <returns>A failure carrying these errors, or a success when there are none.</returns>
    public Result ToResult()
    {
        return this.HasErrors() ? Result.Failure(this) : Result.Success();
    }

    /// <summary>
    /// Turns this collection into a <see cref="Result{TValue}"/> around a value.
    /// </summary>
    /// <typeparam name="TValue">The type the successful result carries.</typeparam>
    /// <param name="value">The value to carry when there are no errors.</param>
    /// <returns>A failure carrying these errors, or a success carrying <paramref name="value"/>.</returns>
    public Result<TValue> ToResult<TValue>(TValue value)
    {
        return this.HasErrors() ? Result<TValue>.Failure(this) : Result<TValue>.Success(value);
    }

    /// <summary>
    /// Runs one of two functions according to whether anything went wrong.
    /// </summary>
    /// <typeparam name="TResult">What both branches return.</typeparam>
    /// <param name="onSuccess">Run when the collection is empty.</param>
    /// <param name="onFailure">Run with the errors when it is not.</param>
    /// <returns>Whatever the branch that ran returned.</returns>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<IReadOnlyErrorCollection, TResult> onFailure)
    {
        return this.HasErrors() ?
            onFailure(this) :
            onSuccess();
    }

    /// <inheritdoc/>
    public Error this[int index] => _errors[index];
}
