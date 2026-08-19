namespace TrainingHub.Shared.Application.Queries;

/// <summary>
/// What a notice needs in order to reach a trainer and address them by name.
/// </summary>
/// <remarks>
/// The address is the account's, not <c>Trainer.ContactEmail</c>. The two are different on purpose:
/// the contact address is published to whoever reads a profile and a trainer may point it at a
/// secretariat or a shared inbox, while the account address belongs to the person who signs in. A
/// sanction is addressed to that person (ADR 0056).
/// </remarks>
/// <param name="EmailAddress">The address of the account the trainer signs in with.</param>
/// <param name="Firstname">The trainer's first name.</param>
/// <param name="Lastname">The trainer's last name.</param>
/// <param name="Language">
/// The language the account reads in, or <see langword="null"/> when it stated none. It rides
/// beside the address for ADR 0091's reason: whoever this notice reaches is resolved here, so
/// the language they read is resolved here too.
/// </param>
public sealed record TrainerAccountDto(
    string EmailAddress,
    string Firstname,
    string Lastname,
    string? Language);

/// <summary>
/// Reads the account behind a trainer, for the notices a sanction produces.
/// </summary>
/// <remarks>
/// <para>
/// It exists because an address is not part of a fact. The welcome message reads the contact
/// address off <c>TrainerCreatedIntegrationEvent</c> legitimately: that address <em>is</em> the
/// registration. A sanction is about standing, and who to tell is a routing detail resolved when
/// the notice is sent — which, behind an outbox, is not when the decision committed (ADR 0056).
/// </para>
/// <para>
/// The inverse of <see cref="ITrainerIdentityQuery"/>, which reads the trainer behind an account so
/// that a token can carry them. Both cross the same seam, in opposite directions, and for the same
/// reason the third read port gives: nothing on either path changes a trainer, so nothing on either
/// needs an aggregate.
/// </para>
/// </remarks>
public interface ITrainerAccountQuery
{
    /// <summary>
    /// The account that trainer signs in with, or <see langword="null"/> when the trainer is gone
    /// or has no address on file.
    /// </summary>
    Task<TrainerAccountDto?> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
}
