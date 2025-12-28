using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;

public class DeleteTrainingCommand(Guid id) : ICommand<Result>
{
    public Guid Id { get; init; } = id;
}

public class DeleteTrainerCommandHandler(ITrainingRepository trainingRepository)
    : ICommandHandler<DeleteTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(request.Id, cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Training with id `{request.Id}` does not exist");
        }

        await trainingRepository.DeleteAsync(new List<Training>() { training }, cancellationToken);
        return Result.Success();
    }
}
