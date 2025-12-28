using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents a collection of <see cref="Error"/> objects, and provides methods for adding and retrieving errors.
/// </summary>
public interface IErrorCollection
{
    /// <summary>
    /// Adds an <see cref="Error"/> to the collection.
    /// </summary>
    /// <param name="error">The error to add to the collection. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    void Add(Error error);

    /// <summary>
    /// Adds an error to the collection by specifying an error code and an error message.
    /// </summary>
    /// <param name="errorCode">The <see cref="ErrorCode"/> associated with the error.</param>
    /// <param name="errorMessage">The error message associated with the error.</param>
    void Add(ErrorCode errorCode, string errorMessage);

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
    /// Add the <see cref="Error"/> objects from the specified <see cref="Result"/> to the collection.
    /// </summary>
    void AddErrors(Result result);

    // The Item property provides methods to read and edit entries in the List.

    Error this[int index]
    {
        get;
        set;
    }
}
