using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Answers whether a trainer's account has proven control of its email address.
/// </summary>
/// <remarks>
/// <para>
/// The fourth port of the family <see cref="IUniquenessTitleChecker"/>,
/// <see cref="ITrainingCounter"/> and <see cref="ITrainerStanding"/> opened (ADR 0030), and the
/// same division of labor: the rule — an unverified trainer may not grow what the public can
/// see — belongs to <see cref="Training"/> and stays there, while the fact it turns on lives in
/// the identity store, which the aggregate cannot see and must not name. The port says
/// "verification", not "email verification", for the reason the ubiquitous language gives: the
/// mailbox is Identity's mechanics, and the domain's fact is that the account behind this
/// trainer is verified (ADR 0090).
/// </para>
/// <para>
/// Only two decisions ask it — creating a training and receiving one by transfer — because they
/// are the only ways a catalog grows from nothing. Publishing, editing and giving away are never
/// asked: verification is granted once and never revoked, so an unverified trainer cannot own a
/// training to publish, edit or give. The day that invariant breaks — a re-verification on an
/// account email change, say — those decisions gain the question (ADR 0090).
/// </para>
/// </remarks>
public interface ITrainerVerification
{
    /// <summary>
    /// Whether the given trainer's account has verified its email address.
    /// </summary>
    /// <remarks>
    /// A trainer no account answers to is not verified: this port reports the proof, and a
    /// missing account is the absence of one.
    /// </remarks>
    /// <param name="trainerId">The trainer whose account's proof is in question.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns><see langword="true"/> when that trainer's account is verified.</returns>
    Task<bool> IsVerifiedAsync(TrainerId trainerId, CancellationToken cancellationToken = default);
}
