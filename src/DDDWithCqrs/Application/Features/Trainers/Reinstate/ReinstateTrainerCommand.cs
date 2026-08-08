using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Reinstate;

/// <summary>
/// Asks that a trainer's sanction be lifted (ADR 0050).
/// </summary>
public sealed class ReinstateTrainerCommand(Guid trainerId) : ICommand<Result>
{
    /// <summary>
    /// The trainer coming back.
    /// </summary>
    public Guid TrainerId { get; init; } = trainerId;
}

/// <summary>
/// Runs <see cref="ReinstateTrainerCommand"/>.
/// </summary>
/// <remarks>
/// No reason, and none is cleared here: the aggregate clears the one it was holding, because a
/// reason outliving the state it motivates is the orphan half of ADR 0052's invariant. Nothing is
/// restored either — the trainer's catalogue comes back exactly as they left it, which is the
/// scenario that decided the shape of ADR 0050.
/// </remarks>
public sealed class ReinstateTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReinstateTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(ReinstateTrainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(request.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Trainer with id `{request.TrainerId}` could not be found.");
        }

        return await trainer.Reinstate().MatchAsync(
            onSuccess: async () =>
            {
                trainerRepository.Update(trainer);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
