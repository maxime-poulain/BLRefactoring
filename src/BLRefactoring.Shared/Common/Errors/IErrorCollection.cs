using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents a collection of <see cref="Error"/> objects, and provides methods for adding and retrieving errors.
/// </summary>
public interface IErrorCollection : IReadOnlyErrorCollection
{
    /// <summary>
    /// Adds an <see cref="Error"/> to the collection.
    /// </summary>
    /// <param name="error">The error to add to the collection. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    void Add(Error error);

    /// <summary>
    /// Adds a collection of <see cref="Error"/> objects to the collection.
    /// </summary>
    /// <param name="errors">The collection of errors to add to the collection. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    void AddErrors(IEnumerable<Error> errors);

    /// <summary>
    /// Returns an <see cref="IReadOnlyErrorCollection"/> that represents this error collection.
    /// </summary>
    /// <returns>An <see cref="IReadOnlyErrorCollection"/> that represents this error collection.</returns>
    IReadOnlyErrorCollection AsReadOnly();

    /// <summary>
    /// Add the <see cref="Error"/> objects from the specified <see cref="IResult"/> to the collection.
    /// </summary>
    void AddErrors(IResult result);
}
