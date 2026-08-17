using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Erase;

/// <summary>
/// Asks that the calling trainer be erased: the profile, and — through the cascade the deletion
/// announces — every training they own (ADR 0085).
/// </summary>
/// <remarks>
/// The trainer's half of the account erasure. Who may ask, the password that proves it, and the
/// account row that goes with it are all the boundary's business; this command stages the
/// aggregate's departure and commits, inside whatever transaction the caller opened around both
/// halves.
/// </remarks>
public sealed class EraseCurrentTrainerCommand : ICommand<Result>;

/// <summary>
/// Runs <see cref="EraseCurrentTrainerCommand"/>.
/// </summary>
public sealed class EraseCurrentTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<EraseCurrentTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(EraseCurrentTrainerCommand request, CancellationToken cancellationToken)
    {
        var id = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer == null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        // The aggregate announces its own disappearance before the repository stages it: the
        // cascade that removes the trainings and the policy that publishes the portrait's
        // collection both hang off this event (ADR 0085).
        trainer.MarkForDeletion();

        trainerRepository.Delete(trainer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
