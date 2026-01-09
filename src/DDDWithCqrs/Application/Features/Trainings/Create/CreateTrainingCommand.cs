using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public class CreateTrainingCommand : ICommand<Result>
{
    [JsonIgnore] public Guid TrainingId { get; init; } = Guid.NewGuid();
    public Guid TrainerId { get; init; }
    public string Title { get; init; } = null!;
    public List<string> Topics { get; init; } = [];
    public string Description { get; init; } = null!;
    public string Prerequisites { get; init; } = null!;
    public string AcquiredSkills { get; init; } = null!;
}

public class CreateTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker titleChecker)
    : ICommandHandler<CreateTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(
        CreateTrainingCommand request,
        CancellationToken cancellationToken)
    {
        var trainer = await trainerRepository.GetByIdAsync(request.TrainerId, cancellationToken);

        if (trainer == null)
        {
            return Result.Failure(ErrorCode.NotFound,
                $"Trainer `{request.TrainerId}` was not found");
        }

        var trainingCreationMessage = new TrainingCreationMessage
        {
            Title = request.Title,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills,
            TrainerId = request.TrainerId,
            Topics = request.Topics,
            UserId = Guid.Empty
        };

        var trainingCreationResult = await Training.CreateAsync(trainingCreationMessage, titleChecker);

        return await trainingCreationResult.MatchAsync<Result>(async (training) =>
        {
            await trainingRepository.SaveAsync(training, cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
