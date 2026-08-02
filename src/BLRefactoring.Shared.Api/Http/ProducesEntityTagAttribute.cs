namespace BLRefactoring.Shared.Api.Http;

/// <summary>
/// Marks an action that publishes the resource's version as an <c>ETag</c>.
/// </summary>
/// <remarks>
/// Purely declarative: it changes nothing at runtime — <see cref="ConcurrencyControllerExtensions.SetETag"/>
/// is what actually sets the header. What it does is put the header in the OpenAPI document, which
/// is the only place a client can learn it exists.
/// <para>
/// That is not a documentation nicety. The <c>If-Match</c> half of this contract was invisible to
/// the document for exactly one reason — nobody declared it — and the generated client therefore
/// could not send it, so every edit from the front end came back 428. Declaring the request half
/// and leaving the response half implicit would repeat the same mistake in miniature: a caller
/// would know it must send a version, and not where to get one. See ADR 0010.
/// </para>
/// <para>
/// An attribute rather than a convention over verbs, because emitting an <c>ETag</c> is a choice
/// each action makes: on this API two of the reads publish one and the collection endpoints do not.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProducesEntityTagAttribute : Attribute;
