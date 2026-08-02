using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;

public sealed class DeleteTrainingCommand(Guid id) : ICommand<Result>
{
    public Guid Id { get; init; } = id;
}

public sealed class DeleteTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(request.Id), cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Training with id `{request.Id}` does not exist");
        }

        trainingRepository.Delete(training);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
