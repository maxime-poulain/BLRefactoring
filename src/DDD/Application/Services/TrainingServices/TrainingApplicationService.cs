using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDD.Application.Services.TrainingServices;

// A good alternative would have been to have one application service per use case.
// This would have allowed us to have a more granular control over the dependencies.
// Also it makes easier to understand what the underlying class does.
// Example: `ITrainingCreator` or `ITrainingCreationService` is more meaningful
// than `ITrainingApplicationService`.

public interface ITrainingApplicationService
{
    Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request, CancellationToken cancellationToken = default);
    Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<TrainingDto>> EditAsync(TrainingEditionRequest request, CancellationToken cancellationToken = default);
}

public class TrainingApplicationService(
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker uniquenessTitleChecker,
    ITrainingRepository trainingRepository,
    ICurrentUserService currentUserService)
    : ITrainingApplicationService
{
    public async Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(currentUserService.TrainerId, cancellationToken);

        if (trainer is null)
        {
            return Result<TrainingDto>.Failure(
                ErrorCode.Unspecified,
                $"Trainer with id `{currentUserService.TrainerId}` not found.");
        }

        var trainingCreationMessage = new TrainingCreationMessage
        {
            Title = request.Title,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills,
            TrainerId = currentUserService.TrainerId,
            Topics = request.Topics,
            UserId = currentUserService.UserId
        };

        var result = await Training.CreateAsync(trainingCreationMessage, uniquenessTitleChecker, cancellationToken);

        return await result.MatchAsync(async training =>
        {
            await trainingRepository.SaveAsync(training, cancellationToken);
            return Result<TrainingDto>.Success(training.ToDto());
        }, Result<TrainingDto>.FailureAsync);
    }

    public async Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync((TrainingId)id, cancellationToken);

        return training is null
            ? Result<TrainingDto>.Failure(ErrorCode.NotFound, $"Training with id `{id}` not found.")
            : Result<TrainingDto>.Success(training.ToDto());
    }

    public async Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return (await trainingRepository.GetAllAsync(cancellationToken)).ToDtos();
    }

    public async Task<Result<TrainingDto>> EditAsync(TrainingEditionRequest request, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync((TrainingId)request.TrainingId, cancellationToken);

        if (training is null)
        {
            return Result<TrainingDto>.Failure(
                ErrorCode.NotFound,
                $"Training with id `{request.TrainingId}` not found.");
        }

        var result = await training.EditAsync(
            new TrainingEditionMessage
            {
                Title = request.Title,
                Description = request.Description,
                Prerequisites = request.Prerequisites,
                AcquiredSkills = request.AcquiredSkills,
                Topics = request.Topics
            },
            uniquenessTitleChecker,
            cancellationToken);

        return result.Match(
            onSuccess: () => Result<TrainingDto>.Success(training.ToDto()),
            onFailure: Result<TrainingDto>.Failure);
    }
}
