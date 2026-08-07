namespace TrainingHub.Shared.Common;

/// <summary>
/// Raised when an identifier factory is handed <see cref="Guid.Empty"/>.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the bare <see cref="ArgumentException"/> the constructor used to raise, and it
/// derives from it because that is what the failure is: an argument the type cannot accept. What
/// the base type could not do is say <em>which</em> failure. In a log, an <c>ArgumentException</c>
/// from here is indistinguishable from every other bad argument in the framework; in a test, an
/// assertion on it also passes when the code throws for an unrelated reason. A name is greppable,
/// alertable, and assertable.
/// </para>
/// <para>
/// <strong>It stays a 500, deliberately.</strong> ADR 0046 settled what this throw means: a caller
/// inside the solution handing an identifier factory an empty value is a programming error, not a
/// bad request, and 500 is the honest answer to a programming error. Mapping it to a 4xx was
/// weighed and refused for two reasons. Nothing at the point of the throw knows where the empty
/// value came from — a token claim, a database row, an outbox payload, a background service — so a
/// 400 would blame a caller who may have sent a perfectly good request. And a guard that goes
/// missing surfaces today as a loud 500; behind a translation it would surface as an ordinary
/// client error, which is the failure mode the guards in ADR 0046 exist to keep visible.
/// </para>
/// <para>
/// Reaching it over HTTP takes a hole in those guards: the contract refuses the empty identifier at
/// model binding and the validator refuses it at dispatch. That is the point — if this is ever seen
/// in production, the name is what makes the hole findable.
/// </para>
/// </remarks>
public sealed class EmptyIdentifierException : ArgumentException
{
    /// <summary>
    /// Initializes the exception for an identifier type that was handed an empty value.
    /// </summary>
    /// <param name="identifierType">The name of the identifier type that refused the value.</param>
    /// <param name="paramName">The name of the offending parameter.</param>
    public EmptyIdentifierException(string identifierType, string paramName)
        : base($"A {identifierType} cannot be empty.", paramName) =>
        IdentifierType = identifierType;

    /// <summary>
    /// Gets the identifier type that refused the value, as data rather than only inside the message.
    /// </summary>
    /// <remarks>
    /// Serilog destructures an exception's public properties, so this reaches the log as a field a
    /// query can group by — "how often, and on which identifier" — instead of a string somebody has
    /// to parse (ADR 0026, ADR 0027).
    /// </remarks>
    public string IdentifierType { get; }
}
