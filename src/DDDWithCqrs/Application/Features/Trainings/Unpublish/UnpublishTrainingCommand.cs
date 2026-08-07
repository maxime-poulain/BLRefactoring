using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Unpublish;

/// <summary>
/// Asks that a training be withdrawn from public view (ADR 0050).
/// </summary>
public sealed class UnpublishTrainingCommand(Guid trainingId) : ICommand<Result>
{
    /// <summary>
    /// The training to withdraw.
    /// </summary>
    public Guid TrainingId { get; init; } = trainingId;
}

/// <summary>
/// Runs <see cref="UnpublishTrainingCommand"/>.
/// </summary>
/// <remarks>
/// Takes no standing port and no counter, and that asymmetry with publishing is the record's rule
/// showing through: withdrawing shrinks what the public can see, so nothing about the owner's
/// standing or the size of their catalogue can refuse it.
/// </remarks>
public sealed class UnpublishTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UnpublishTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(UnpublishTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(request.TrainingId), cancellationToken);

        if (training is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Training with id `{request.TrainingId}` does not exist");
        }

        var result = training.Unpublish();

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
