namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents a read-only collection of <see cref="Error"/> objects, and provides a method for checking whether the collection contains any errors.
/// </summary>
public interface IReadOnlyErrorCollection : IEnumerable<Error>
{
    // The Item property provides methods to read and edit entries in the List.
    Error this[int index]
    {
        get;
    }
}
