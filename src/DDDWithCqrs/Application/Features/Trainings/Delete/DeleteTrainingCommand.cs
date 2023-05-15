using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;

public class DeleteTrainingCommand : ICommand<Result>
{
    public DeleteTrainingCommand(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; init; }
}

public class DeleteTrainerCommandHandler : ICommandHandler<DeleteTrainingCommand, Result>
{
    private readonly ITrainingRepository _trainingRepository;

    public DeleteTrainerCommandHandler(ITrainingRepository trainingRepository)
    {
        _trainingRepository = trainingRepository;
    }

    public async Task<Result> Handle(DeleteTrainingCommand request, CancellationToken cancellationToken)
    {
        var training = await _trainingRepository.GetByIdAsync(request.Id, cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Training with id `{request.Id}` does not exist");
        }

        await _trainingRepository.DeleteAsync(new List<Training>() { training }, cancellationToken);
        return Result.Success();
    }
}
