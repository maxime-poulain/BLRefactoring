using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Withhold;

/// <summary>
/// Asks that a training be taken out of public view by the administration, with a stated reason
/// (ADR 0052).
/// </summary>
public sealed class WithholdTrainingCommand(Guid trainingId, string reason) : ICommand<Result>
{
    /// <summary>
    /// The training being withheld.
    /// </summary>
    public Guid TrainingId { get; init; } = trainingId;

    /// <summary>
    /// Why, as the caller wrote it. Judged by <see cref="WithholdingReason"/>.
    /// </summary>
    public string Reason { get; init; } = reason;
}

/// <summary>
/// Runs <see cref="WithholdTrainingCommand"/>.
/// </summary>
/// <remarks>
/// No ownership question is asked, and that is the whole point of the state: withholding is what an
/// administrator does to somebody else's training, and the owner cannot undo it by either door.
/// Every rule is the aggregate's — already withheld is its refusal to make — so this handler loads,
/// asks, and saves.
/// </remarks>
public sealed class WithholdTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<WithholdTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(WithholdTrainingCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var training = await trainingRepository.GetByIdAsync(
            TrainingId.Create(request.TrainingId), cancellationToken);

        if (training is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Training with id `{request.TrainingId}` does not exist");
        }

        var reasonResult = WithholdingReason.Create(request.Reason);

        return await reasonResult.MatchAsync<Result>(
            onSuccess: async reason => await training.Withhold(reason).MatchAsync(
                onSuccess: async () =>
                {
                    trainingRepository.Update(training);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return Result.Success();
                },
                onFailure: Result.FailureAsync),
            onFailure: Result.FailureAsync);
    }
}
