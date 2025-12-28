using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDD.Application.Services.TrainingServices;

// A good alternative would have been to have one application service per use case.
// This would have allowed us to have a more granular control over the dependencies.
// Also it makes easier to understand what the underlying class does.
// Example: `ITrainingCreator` or `ITrainingCreationService` is more meaningful
// than `ITrainingApplicationService`.

public interface ITrainingApplicationService
{
    Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request);
    Task<Result<TrainingDto>> GetByIdAsync(Guid id);
    Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class TrainingApplicationService(
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker uniquenessTitleChecker,
    ITrainingRepository trainingRepository)
    : ITrainingApplicationService
{
    public async Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request)
    {
        var trainer = await trainerRepository.GetByIdAsync(request.TrainerId);

        if (trainer is null)
        {
            return Result<TrainingDto>.Failure(ErrorCode.Unspecified,
                $"Trainer with id `{request.TrainerId}` not found.");
        }

        var result = await Training.CreateAsync(
            request.Title,
            request.StartDate,
            request.EndDate,
            trainer,
            request.Rates.ToRates(),
            uniquenessTitleChecker);

        return await result.MatchAsync(async training =>
        {
            await trainingRepository.SaveAsync(training);
            return Result<TrainingDto>.Success(training.ToDto());
        }, Result<TrainingDto>.FailureAsync);

    }

    public async Task<Result<TrainingDto>> GetByIdAsync(Guid id)
    {
        var training = await trainingRepository.GetByIdAsync(id);

        return training is null
            ? Result<TrainingDto>.Failure(ErrorCode.NotFound, $"Training with id `{id}` not found.")
            : Result<TrainingDto>.Success(training.ToDto());
    }

    public async Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return (await trainingRepository.GetAllAsync(cancellationToken)).ToDtos();
    }
}
