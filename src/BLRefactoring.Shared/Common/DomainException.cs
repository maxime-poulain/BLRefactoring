using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents the base class for domain exceptions.
/// </summary>
/// <remarks>
/// This class is used as a base class for all domain-specific exceptions in the application.
/// </remarks>
public class DomainException : Exception
{
    /// <summary>
    /// Gets the collection of errors associated with the exception.
    /// </summary>
    public IReadOnlyErrorCollection Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with the specified result.
    /// </summary>
    /// <param name="result">The result containing the errors to associate with the exception.</param>
    public DomainException(Result result)
    {
        Errors = result.Match(
            () => throw new InvalidOperationException("Cannot throw an exception with a result in successful state"),
            errors => errors);
    }
}
