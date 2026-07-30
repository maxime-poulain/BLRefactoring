using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;

/// <summary>
/// Deletes a <see cref="Trainer"/> by its <see cref="Trainer.Id"/>.
/// </summary>
public class DeleteTrainerCommand : ICommand<Result>
{
    public Guid Id { get; set; }

    public DeleteTrainerCommand()
    {
    }

    public DeleteTrainerCommand(Guid id)
    {
        Id = id;
    }
}

public class DeleteTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTrainerCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(request.Id), cancellationToken);

        if (trainer == null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Trainer with id `{request.Id}` does not exist");
        }

        // MarkForDeletion raises a TrainerDeletedDomainEvent. It is dispatched while
        // the unit of work saves, right before persistence: its handler stages the
        // deletion of the trainer's trainings in the same change tracker, so the
        // trainer and its trainings are deleted atomically within the single implicit
        // transaction of SaveChangesAsync. No explicit transaction is required.
        // Unmanaged exceptions bubble up to the API layer.
        trainer.MarkForDeletion();
        trainerRepository.Delete(trainer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
