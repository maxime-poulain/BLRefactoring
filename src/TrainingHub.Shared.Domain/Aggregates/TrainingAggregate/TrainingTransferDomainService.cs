using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Decides whether a training may change hands — the one decision no aggregate can own.
/// </summary>
/// <remarks>
/// The transfer rule reads the <em>recipient's</em> catalog to mutate the <em>giver's</em>
/// training: <see cref="Training"/> cannot answer facts about a set it does not belong to, and
/// <see cref="Trainer"/> holds no trainings. This is the operation ADR 0030 reserved the pattern
/// for and refused at creation because creation had a home; the transfer has none (ADR 0036).
/// Static and stateless like the factory it mirrors: the ports arrive as parameters, so the
/// signature is the complete inventory of what the decision asks of the world, and the domain
/// names no lifetime. <see cref="Training.TransferTo"/> is internal — this service is the only
/// public path to reassignment, which is the compile-time answer to "a service makes the rule
/// forgettable".
/// </remarks>
public static class TrainingTransferDomainService
{
    /// <summary>
    /// Hands a training over to a colleague, when the recipient's catalog allows it: room
    /// under the limit, and no training already listed under the same title.
    /// </summary>
    /// <param name="training">The training changing hands.</param>
    /// <param name="recipient">The trainer receiving it.</param>
    /// <param name="trainingCounter">Answers how many trainings the recipient publishes.</param>
    /// <param name="titleChecker">Answers whether the recipient already lists the title.</param>
    /// <param name="trainerStanding">Answers whether either trainer is under sanction.</param>
    /// <param name="trainerVerification">Answers whether the recipient's account is verified.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public static async Task<Result> TransferAsync(
        Training training,
        TrainerId recipient,
        ITrainingCounter trainingCounter,
        IUniquenessTitleChecker titleChecker,
        ITrainerStanding trainerStanding,
        ITrainerVerification trainerVerification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(trainingCounter);
        ArgumentNullException.ThrowIfNull(titleChecker);
        ArgumentNullException.ThrowIfNull(trainerStanding);
        ArgumentNullException.ThrowIfNull(trainerVerification);

        // The aggregate's own question, asked first: a transfer to the current owner would make
        // the recipient-side rules vacuous and put a lie of a fact on the wire.
        if (training.IsOwnedBy(recipient))
        {
            return Result.Failure(TrainingErrorCodes.TransferToSelf,
                "A training cannot be transferred to the trainer who already owns it.");
        }

        // Both sides are asked, because a transfer moves a training out of one public catalog and
        // into another. The giver is refused for the reason ADR 0050 gives — a suspended trainer
        // may not change what the public sees — and the recipient because a transfer they cannot
        // refuse would grow a catalog the sanction exists to freeze. This is what the record
        // means by "giving and receiving alike"; ADR 0036's "the recipient must be able to accept
        // it" now has a second half.
        if (await trainerStanding.IsSuspendedAsync(training.TrainerId, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.TrainerSuspended,
                "A suspended trainer cannot transfer a training.");
        }

        if (await trainerStanding.IsSuspendedAsync(recipient, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.RecipientSuspended,
                "The recipient is suspended and cannot receive a training.");
        }

        // Only the recipient is asked about verification, and the asymmetry is the invariant:
        // the giver owns a training, and owning one is something an unverified account can never
        // have done — creation asks the same question at the only other door a catalog grows
        // through (ADR 0090). A transfer the recipient's unproven account cannot refuse would
        // grow a catalog verification exists to gate.
        if (!await trainerVerification.IsVerifiedAsync(recipient, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.RecipientUnverified,
                "The recipient is unverified and cannot receive a training.");
        }

        // Capacity before content, mirroring creation: no title makes an eleventh training
        // acceptable, so a full recipient refuses the transfer whole rather than per field.
        var published = await trainingCounter.CountForTrainerAsync(recipient, cancellationToken);
        if (published >= Training.MaximumPerTrainer)
        {
            return Result.Failure(TrainingErrorCodes.RecipientCatalogFull,
                $"The recipient already publishes {Training.MaximumPerTrainer} trainings and cannot receive another.");
        }

        if (await titleChecker.TitleForTrainerExistsAsync(training.Title, recipient, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.DuplicateTitle,
                "The recipient already has a training under that title.");
        }

        training.TransferTo(recipient);
        return Result.Success();
    }
}
