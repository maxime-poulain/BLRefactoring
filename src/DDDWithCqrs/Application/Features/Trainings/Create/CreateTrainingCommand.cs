using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public class CreateTrainingCommand : ICommand<Result>
{
    [JsonIgnore]
    public Guid TrainingId { get; init; } = Guid.NewGuid();
    public Guid TrainerId { get; init; }
    public string Title { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<RateDto> Rates { get; init; } = new();
}

public class CreateTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker checker)
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

        var ratesResult = request.Rates.ToDomainModels();

        return await ratesResult.MatchAsync(async rates =>
        {
            var trainingCreationResult = await Training.CreateAsync((TrainingId)request.TrainingId,
                request.Title,
                request.StartDate,
                request.EndDate,
                trainer,
                rates,
                checker);

            return await trainingCreationResult.MatchAsync<Result>(async (training) =>
            {
                await trainingRepository.SaveAsync(training, cancellationToken);
                return Result.Success();
            }, Result.FailureAsync);

        }, Result.FailureAsync);
    }
}
