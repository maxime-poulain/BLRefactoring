namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents an error with a specific error code and error message, and the time when the error occurred.
/// </summary>
public class Error : ValueObject
{
    /// <summary>
    /// Gets the error message associated with this error.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the error code associated with this error.
    /// </summary>
    public ErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets the time when this error occurred.
    /// </summary>
    public DateTimeOffset OccurredOn { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> class with the specified error code and error message.
    /// </summary>
    /// <param name="errorCode">The error code associated with this error.</param>
    /// <param name="errorMessage">The error message associated with this error.</param>
    public Error(ErrorCode errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        OccurredOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Implements value object equality by returning the components that make up this value object.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ErrorCode;
        yield return ErrorMessage;
        yield return OccurredOn;
    }

    /// <summary>
    /// Returns a string that represents this error.
    /// </summary>
    public override string ToString()
    {
        return $"[{OccurredOn:u}] {ErrorCode.Name}: {ErrorMessage}";
    }
}
