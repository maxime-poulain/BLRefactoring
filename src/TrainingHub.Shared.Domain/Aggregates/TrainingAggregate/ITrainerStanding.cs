using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Answers whether a trainer is under sanction.
/// </summary>
/// <remarks>
/// <para>
/// The third port of the same family as <see cref="IUniquenessTitleChecker"/> and
/// <see cref="ITrainingCounter"/>, and the same division of labor (ADR 0030): the rule — a
/// suspended trainer may not increase their public footprint — belongs to <see cref="Training"/>
/// and stays there, while the fact it turns on sits in a row the aggregate cannot see. So the port
/// answers the bare fact and never the decision; an implementation that answered
/// "may this trainer publish" would own half a rule that is written down here.
/// </para>
/// <para>
/// It lives beside the training rather than beside the trainer because the decisions it feeds are
/// the training's — creating one, publishing one, transferring one — which is where
/// <see cref="ITrainingCounter"/> sits for the same reason, though it too counts something the
/// trainer owns.
/// </para>
/// </remarks>
public interface ITrainerStanding
{
    /// <summary>
    /// Whether the given trainer is currently under sanction.
    /// </summary>
    /// <remarks>
    /// A trainer no row answers to is not suspended: this port reports standing, and reporting
    /// "unknown trainer" through it would make two different refusals share one answer. Where the
    /// recipient of a transfer has to exist, that is asked separately and refused by name.
    /// </remarks>
    /// <param name="trainerId">The trainer whose standing is in question.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns><see langword="true"/> when that trainer is suspended.</returns>
    Task<bool> IsSuspendedAsync(TrainerId trainerId, CancellationToken cancellationToken = default);
}
