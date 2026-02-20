using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

namespace BLRefactoring.DDD.Application.Services.TrainingServices;

// A good alternative would have been to have one application service per use case.
// This would have allowed us to have a more granular control over the dependencies.
// Also it makes easier to understand what the underlying class does.
// Example: `ITrainingCreator` or `ITrainingCreationService` is more meaningful
// than `ITrainingApplicationService`.

/// <summary>
/// Application service interface for managing training operations (create, read, update, delete).
/// </summary>
public interface ITrainingApplicationService
{
    /// <summary>
    /// Creates a new training from the given request.
    /// </summary>
    Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a training by its unique identifier.
    /// </summary>
    Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all available trainings.
    /// </summary>
    Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits an existing training identified by <paramref name="trainingId"/>.
    /// </summary>
    Task<Result<TrainingDto>> EditAsync(Guid trainingId, TrainingEditionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all trainings belonging to the specified trainer.
    /// </summary>
    Task<Result<List<TrainingDto>>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all trainings matching the specified topic name.
    /// </summary>
    Task<List<TrainingDto>> GetByTopicAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a training by its unique identifier.
    /// </summary>
    Task<Result> DeleteAsync(Guid trainingId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="ITrainingApplicationService"/> that orchestrates
/// training CRUD operations using domain services and repositories.
/// </summary>
public class TrainingApplicationService(
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker uniquenessTitleChecker,
    ITrainingRepository trainingRepository,
    ICurrentUserService currentUserService)
    : ITrainingApplicationService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync((TrainingId)id, cancellationToken);

        return training is null
            ? Result<TrainingDto>.Failure(ErrorCode.NotFound, $"Training with id `{id}` not found.")
            : Result<TrainingDto>.Success(training.ToDto());
    }

    /// <inheritdoc />
    public async Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return (await trainingRepository.GetAllAsync(cancellationToken)).ToDtos();
    }

    /// <inheritdoc />
    public async Task<Result<TrainingDto>> EditAsync(Guid trainingId, TrainingEditionRequest request, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync((TrainingId)trainingId, cancellationToken);

        if (training is null)
        {
            return Result<TrainingDto>.Failure(
                ErrorCode.NotFound,
                $"Training with id `{trainingId}` not found.");
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

        return await result.MatchAsync(
            onSuccess: async () =>
            {
                await trainingRepository.SaveAsync(training, cancellationToken);
                return Result<TrainingDto>.Success(training.ToDto());
            },
            onFailure: Result<TrainingDto>.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<Result<List<TrainingDto>>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var trainings = await trainingRepository.GetByTrainerIdAsync((TrainerId)trainerId, cancellationToken);
        return Result<List<TrainingDto>>.Success(trainings.ToDtos());
    }

    /// <inheritdoc />
    public async Task<List<TrainingDto>> GetByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        var spec = new TrainingsByTopicSpecification(topic);
        var trainings = await trainingRepository.GetAsync(spec, cancellationToken);
        return trainings.ToDtos();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync((TrainingId)trainingId, cancellationToken);
        if (training is null)
        {
            return Result.Failure(
                ErrorCode.NotFound,
                $"Training with id `{trainingId}` not found.");
        }

        await trainingRepository.DeleteAsync(training, cancellationToken);
        return Result.Success();
    }
}
