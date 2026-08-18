using TrainingHub.Shared;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Transfer;

/// <summary>
/// Asks that a training be handed over to another trainer (ADR 0036).
/// </summary>
public sealed class TransferTrainingCommand(Guid trainingId, Guid recipientTrainerId) : ICommand<Result>
{
    /// <summary>
    /// The training changing hands.
    /// </summary>
    public Guid TrainingId { get; init; } = trainingId;

    /// <summary>
    /// The trainer receiving it.
    /// </summary>
    public Guid RecipientTrainerId { get; init; } = recipientTrainerId;
}

/// <summary>
/// Runs <see cref="TransferTrainingCommand"/>.
/// </summary>
/// <remarks>
/// The decision itself lives in <see cref="TrainingTransferDomainService"/>, the domain's one recorded
/// service; this handler orchestrates — loads the training, refuses a ghost recipient (existence
/// is referential integrity, not part of the transfer decision), and saves under the unique
/// index that closes the title race for real.
/// </remarks>
public sealed class TransferTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    ITrainerRepository trainerRepository,
    ITrainingCounter trainingCounter,
    IUniquenessTitleChecker uniquenessTitleChecker,
    ITrainerStanding trainerStanding,
    ITrainerVerification trainerVerification,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TransferTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(TransferTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(request.TrainingId), cancellationToken);
        if (training is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Training with id `{request.TrainingId}` does not exist");
        }

        var recipient = TrainerId.Create(request.RecipientTrainerId);
        if (!await trainerRepository.ExistsAsync(recipient, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.UnknownRecipient,
                $"No trainer with id `{request.RecipientTrainerId}` exists to receive the training.");
        }

        var result = await TrainingTransferDomainService.TransferAsync(
            training,
            recipient,
            trainingCounter,
            uniquenessTitleChecker,
            trainerStanding,
            trainerVerification,
            cancellationToken);

        return await result.MatchAsync(
            onSuccess: async () =>
            {
                trainingRepository.Update(training);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (UniqueConstraintViolationException)
                {
                    // A concurrent request slipped past the uniqueness pre-check;
                    // the unique index is the authoritative guard, so a lost race
                    // is the same business failure as a detected duplicate.
                    return Result.Failure(TrainingErrorCodes.DuplicateTitle,
                        "The recipient already has a training under that title.");
                }
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
