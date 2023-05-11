namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents a read-only collection of <see cref="Error"/> objects, and provides a method for checking whether the collection contains any errors.
/// </summary>
public interface IReadOnlyErrorCollection : IEnumerable<Error>
{
    /// <summary>
    /// Returns a value indicating whether this error collection contains any errors.
    /// </summary>
    /// <returns><see langword="true"/> if this error collection contains errors; otherwise, <see langword="false"/>.</returns>
    bool HasErrors();
}
