using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Decides whether a training may change hands — the one decision no aggregate can own.
/// </summary>
/// <remarks>
/// The transfer rule reads the <em>recipient's</em> catalogue to mutate the <em>giver's</em>
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
    /// Hands a training over to a colleague, when the recipient's catalogue allows it: room
    /// under the limit, and no training already listed under the same title.
    /// </summary>
    /// <param name="training">The training changing hands.</param>
    /// <param name="recipient">The trainer receiving it.</param>
    /// <param name="trainingCounter">Answers how many trainings the recipient publishes.</param>
    /// <param name="titleChecker">Answers whether the recipient already lists the title.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public static async Task<Result> TransferAsync(
        Training training,
        TrainerId recipient,
        ITrainingCounter trainingCounter,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(trainingCounter);
        ArgumentNullException.ThrowIfNull(titleChecker);

        // The aggregate's own question, asked first: a transfer to the current owner would make
        // the recipient-side rules vacuous and put a lie of a fact on the wire.
        if (training.IsOwnedBy(recipient))
        {
            return Result.Failure(TrainingErrorCodes.TransferToSelf,
                "A training cannot be transferred to the trainer who already owns it.");
        }

        // Capacity before content, mirroring creation: no title makes an eleventh training
        // acceptable, so a full recipient refuses the transfer whole rather than per field.
        var published = await trainingCounter.CountForTrainerAsync(recipient, cancellationToken);
        if (published >= Training.MaximumPerTrainer)
        {
            return Result.Failure(TrainingErrorCodes.RecipientCatalogueFull,
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
