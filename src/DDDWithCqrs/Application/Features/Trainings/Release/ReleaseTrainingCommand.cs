using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Release;

/// <summary>
/// Asks that a withheld training be released (ADR 0052).
/// </summary>
public sealed class ReleaseTrainingCommand(Guid trainingId) : ICommand<Result>
{
    /// <summary>
    /// The training being released.
    /// </summary>
    public Guid TrainingId { get; init; } = trainingId;
}

/// <summary>
/// Runs <see cref="ReleaseTrainingCommand"/>.
/// </summary>
/// <remarks>
/// The training lands on <c>Unpublished</c>, never back on <c>Published</c>: lifting an interdiction
/// is not deciding to put a training back in the window, and publishing becomes the owner's call
/// again. The reason is cleared by the aggregate, which is the half of ADR 0052's invariant that
/// forbids an orphan reason.
/// </remarks>
public sealed class ReleaseTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReleaseTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(ReleaseTrainingCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var training = await trainingRepository.GetByIdAsync(
            TrainingId.Create(request.TrainingId), cancellationToken);

        if (training is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Training with id `{request.TrainingId}` does not exist");
        }

        return await training.Release().MatchAsync(
            onSuccess: async () =>
            {
                trainingRepository.Update(training);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
