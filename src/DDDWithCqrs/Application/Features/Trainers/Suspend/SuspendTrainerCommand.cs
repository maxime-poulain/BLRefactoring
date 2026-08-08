using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Suspend;

/// <summary>
/// Asks that a trainer be placed under sanction, with a stated reason (ADR 0052).
/// </summary>
public sealed class SuspendTrainerCommand(Guid trainerId, string reason) : ICommand<Result>
{
    /// <summary>
    /// The trainer being sanctioned.
    /// </summary>
    public Guid TrainerId { get; init; } = trainerId;

    /// <summary>
    /// Why, as the caller wrote it. Judged by <see cref="SuspensionReason"/>.
    /// </summary>
    public string Reason { get; init; } = reason;
}

/// <summary>
/// Runs <see cref="SuspendTrainerCommand"/>.
/// </summary>
/// <remarks>
/// The trainer is carried on the command rather than resolved from the caller, which is what makes
/// this an administrative use case rather than a self-service one: every other trainer command on
/// this stack reads <c>ICurrentUserService.TrainerId</c> and can therefore only ever touch the
/// caller's own profile. Who is entitled to name somebody else is settled at the API boundary and
/// nowhere else, and this layer never asks — it could not name the authority that answers without
/// depending on the API to do so (ADR 0051).
/// </remarks>
public sealed class SuspendTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SuspendTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(SuspendTrainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(request.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Trainer with id `{request.TrainerId}` could not be found.");
        }

        var reasonResult = SuspensionReason.Create(request.Reason);

        return await reasonResult.MatchAsync<Result>(
            onSuccess: async reason => await trainer.Suspend(reason).MatchAsync(
                onSuccess: async () =>
                {
                    trainerRepository.Update(trainer);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return Result.Success();
                },
                onFailure: Result.FailureAsync),
            onFailure: Result.FailureAsync);
    }
}
