using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Delete;

/// <summary>
/// Asks that a training be deleted.
/// </summary>
public sealed class DeleteTrainingCommand(Guid id) : ICommand<Result>
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public Guid Id { get; init; } = id;
}

/// <summary>
/// Runs <see cref="DeleteTrainingCommand"/>.
/// </summary>
public sealed class DeleteTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTrainingCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(DeleteTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(request.Id), cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Training with id `{request.Id}` does not exist");
        }

        // The aggregate announces its own disappearance before the repository stages it. Deleting
        // used to raise nothing at all, which is how a training could vanish from the database and
        // stay in the search index for ever (ADR 0050).
        training.MarkForDeletion();

        trainingRepository.Delete(training);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
