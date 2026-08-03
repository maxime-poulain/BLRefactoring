using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Storage;

/// <summary>
/// Names one object in an <see cref="IObjectStore"/>.
/// </summary>
/// <remarks>
/// Opaque on purpose. The store treats this as a name and nothing else — it does not parse it, and
/// nothing outside the infrastructure builds one. Keeping it a type rather than a bare string is
/// what stops a caller from passing a bucket, a URL or a trainer identifier by accident, all three
/// of which are strings the compiler would otherwise be happy with.
/// </remarks>
public sealed class ObjectKey : ValueObject
{
    /// <summary>
    /// The name itself.
    /// </summary>
    public string Value { get; }

    private ObjectKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Builds a key.
    /// </summary>
    /// <param name="value">The name of the object.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    /// <remarks>
    /// This throws rather than returning a result because a blank key is a programming mistake,
    /// not something a user typed: keys are composed in the infrastructure from identifiers that
    /// already exist.
    /// </remarks>
    public static ObjectKey Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new ObjectKey(value);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
