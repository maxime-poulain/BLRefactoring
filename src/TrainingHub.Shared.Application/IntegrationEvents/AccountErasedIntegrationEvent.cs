namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// An account erased itself — the fact, committed by the endpoint inside the erasing transaction.
/// </summary>
/// <remarks>
/// The third fact with no aggregate behind it, beside the visitor's message and the recovery pair:
/// the account is the framework's, so no domain event precedes it, and the erasure endpoint —
/// which is holding the <c>IdentityUser</c> it just checked a password against — commits it
/// directly (ADR 0085). The address and username travel on the fact for a stronger reason than
/// usual: the sanction notices resolve an address at delivery because the row will still be there,
/// and this fact is only ever true once the row is not. The farewell carries its own destination
/// or nobody is told.
/// </remarks>
/// <param name="UserId">The account that was erased.</param>
/// <param name="Email">The erased account's address, where the farewell goes.</param>
/// <param name="Username">The erased account's username, for the message's greeting.</param>
/// <param name="Language">
/// The language the recipient reads in, as the account stated it, or <see langword="null"/> when
/// it stated none. It rides the fact for the reason the address does: whoever receives this notice
/// is resolved here and nowhere else, so their language is resolved here too (ADR 0091).
/// </param>
public sealed record AccountErasedIntegrationEvent(
    Guid UserId,
    string Email,
    string Username,
    string? Language) : IIntegrationEvent;
