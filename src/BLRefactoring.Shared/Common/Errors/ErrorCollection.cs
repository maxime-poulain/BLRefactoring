using System.Collections;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Common.Errors;

/// <inheritdoc cref="IErrorCollection"/>
public sealed class ErrorCollection : IErrorCollection, IReadOnlyErrorCollection
{
    private readonly List<Error> _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCollection"/> class.
    /// </summary>
    public ErrorCollection()
    {
        _errors = new List<Error>();
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

    /// <inheritdoc/>
    public void Add(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _errors.Add(error);
    }

    public void Add(ErrorCode errorCode, string errorMessage)
    {
        Add(new Error(errorCode, errorMessage));
    }

    /// <inheritdoc/>
    public void AddErrors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors, nameof(errors));
        _errors.AddRange(errors);
    }

    /// <inheritdoc/>
    public void AddErrors(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        AddErrors(result.Errors);
    }

    /// <inheritdoc/>
    public bool HasErrors()
    {
        return _errors.Any();
    }

    /// <inheritdoc/>
    public IReadOnlyErrorCollection AsReadOnly()
    {
        return this;
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
}
