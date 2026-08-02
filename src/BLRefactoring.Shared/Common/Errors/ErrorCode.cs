namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Identifies the kind of an <see cref="Error"/>, independently of the message carried alongside it.
/// </summary>
/// <remarks>
/// The code is the stable half of an error. It is what a log is grepped for, what the API maps to a
/// status code, and what a client branches on. The message may be reworded freely — the code may
/// not, because somebody downstream is matching on it.
/// <para>
/// Codes are declared by whoever raises them, not by this kernel. The kernel defines only what a
/// code <em>is</em>; the vocabulary belongs to the aggregate that owns the rule being broken.
/// Declare them as <c>static readonly</c> fields on a holder class named <c>*ErrorCodes</c>:
/// </para>
/// <code>
/// public static class TrainingErrorCodes
/// {
///     public static readonly ErrorCode DuplicateTitle = new("Training.DuplicateTitle");
/// }
/// </code>
/// <para>
/// Prefix each code with the name of its owner, so that no two owners can claim the same code and
/// so that a code is self-describing wherever it surfaces. The three codes this kernel declares
/// carry no prefix, and that is the point of them: "not found" and "concurrency conflict" are true
/// of any aggregate, so they belong to none.
/// </para>
/// <para>
/// The constructor is public, which makes the set open. That is the price of letting each owner
/// declare its own, and it is why <c>ErrorVocabularyRules</c> exists: nothing in the language stops
/// a caller from constructing a code inline at a call site, misspelling it, and shipping a code no
/// holder declares — so a test does. Note the phrasing here, which deliberately does not spell the
/// construction it forbids: that rule reads source, and it would find its own explanation.
/// </para>
/// </remarks>
public sealed class ErrorCode : ValueObject
{
    /// <summary>
    /// The textual value of the code, for example <c>Training.DuplicateTitle</c>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a code.
    /// </summary>
    /// <param name="value">The code's value, conventionally <c>&lt;Owner&gt;.&lt;Reason&gt;</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or white space.</exception>
    public ErrorCode(string value)
    {
        // A guard, not a business rule: a blank code is a programming mistake, and the kind that
        // only surfaces in a log nobody can grep six months later.
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
    }

    /// <summary>
    /// Implements value object equality by returning the components that make up this value object.
    /// </summary>
    /// <remarks>
    /// Equality by value rather than by reference is what makes an open set safe to compare: two
    /// codes carrying the same string are the same code, whoever built them. The smart-enum version
    /// this replaces compared correctly only because every instance happened to be a singleton.
    /// </remarks>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Returns the textual value of this code.</summary>
    public override string ToString() => Value;
}
