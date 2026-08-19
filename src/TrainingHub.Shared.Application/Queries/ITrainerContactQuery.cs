namespace TrainingHub.Shared.Application.Queries;

/// <summary>
/// What a message from a visitor needs in order to reach a trainer and address them by name.
/// </summary>
/// <remarks>
/// The address is <c>Trainer.ContactEmail</c>, not the account's, and that is the whole reason this
/// port exists beside <see cref="ITrainerAccountQuery"/> rather than being folded into it. The
/// aggregate says it plainly: the contact address is a business attribute the trainer publishes for
/// exactly this, while the account address is the credential the person signs in with. A visitor is
/// answering the published one; they have no business reaching the other (ADR 0082).
/// </remarks>
/// <param name="EmailAddress">The address the trainer publishes for being contacted.</param>
/// <param name="Firstname">The trainer's first name.</param>
/// <param name="Lastname">The trainer's last name.</param>
/// <param name="Language">
/// The language the account reads in, or <see langword="null"/> when it stated none. It rides
/// beside the address for ADR 0091's reason: whoever this notice reaches is resolved here, so
/// the language they read is resolved here too.
/// </param>
public sealed record TrainerContactDto(
    string EmailAddress,
    string Firstname,
    string Lastname,
    string? Language);

/// <summary>
/// Reads the address a trainer publishes for being contacted, for the message a visitor sends.
/// </summary>
/// <remarks>
/// <para>
/// The mirror of <see cref="ITrainerAccountQuery"/>, and deliberately its opposite: a sanction is
/// addressed to the person who signs in, a visitor's message to the address that person put on
/// their profile. ADR 0056 fixed the first; ADR 0082 fixes the second, and the two ports keep the
/// choice from being made by whichever address happened to be in scope.
/// </para>
/// <para>
/// It answers at delivery rather than traveling on the fact, for the reason ADR 0056 already gave:
/// who to tell is a routing detail, resolved when the notice is sent — which, behind an outbox, is
/// not when the visitor pressed send. Here it buys something more: the address is never written to
/// the outbox, so the only place in this application that holds it at all is the moment of sending
/// (ADR 0082).
/// </para>
/// </remarks>
public interface ITrainerContactQuery
{
    /// <summary>
    /// The address that trainer publishes for being contacted, or <see langword="null"/> when the
    /// trainer is gone.
    /// </summary>
    Task<TrainerContactDto?> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
}
