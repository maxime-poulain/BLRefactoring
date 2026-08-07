using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Publish;

/// <summary>
/// Asks that a withdrawn training be offered to the public again (ADR 0050).
/// </summary>
public sealed class PublishTrainingCommand(Guid trainingId) : ICommand<Result>
{
    /// <summary>
    /// The training to publish.
    /// </summary>
    public Guid TrainingId { get; init; } = trainingId;
}

/// <summary>
/// Runs <see cref="PublishTrainingCommand"/>.
/// </summary>
/// <remarks>
/// Every rule is the aggregate's — already published, owner suspended, catalogue full — so this
/// handler loads, asks, and saves. It answers a bare <see cref="Result"/> like every command: what
/// the training now looks like is read back with a query.
/// </remarks>
public sealed class PublishTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    ITrainerStanding trainerStanding,
    ITrainingCounter trainingCounter,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(PublishTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(request.TrainingId), cancellationToken);

        if (training is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Training with id `{request.TrainingId}` does not exist");
        }

        var result = await training.PublishAsync(trainerStanding, trainingCounter, cancellationToken);

        return await result.MatchAsync(
            onSuccess: async () =>
            {
                trainingRepository.Update(training);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
