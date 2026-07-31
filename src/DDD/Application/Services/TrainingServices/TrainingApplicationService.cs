using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

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
    Task<Result<TrainingDto>> EditAsync(Guid trainingId, TrainingEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default);

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
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ITrainingApplicationService
{
    /// <inheritdoc />
    public async Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(currentUserService.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainingDto>.Failure(
                ErrorCode.Unspecified,
                $"Trainer with id `{currentUserService.TrainerId}` not found.");
        }

        var detailsResult = TrainingDetailsFactory.Create(
            request.Title, request.Description, request.Prerequisites, request.AcquiredSkills, request.Topics);

        var result = await detailsResult.MatchAsync(
            async details => await Training.CreateAsync(
                TrainingId.Generate(),
                TrainerId.Create(currentUserService.TrainerId),
                details.Title,
                details.Description,
                details.Prerequisites,
                details.AcquiredSkills,
                details.Topics,
                uniquenessTitleChecker,
                cancellationToken),
            Result<Training>.FailureAsync);

        return await result.MatchAsync(async training =>
        {
            trainingRepository.Add(training);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent request slipped past the uniqueness pre-check;
                // the unique index is the authoritative guard, so a lost race
                // is the same business failure as a detected duplicate.
                return Result<TrainingDto>.Failure(ErrorCode.DuplicateTitle,
                    "A training with the same title already exists for this trainer.");
            }
            return Result<TrainingDto>.Success(training.ToDto());
        }, Result<TrainingDto>.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(id), cancellationToken);

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
    public async Task<Result<TrainingDto>> EditAsync(Guid trainingId, TrainingEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(trainingId), cancellationToken);

        if (training is null)
        {
            return Result<TrainingDto>.Failure(
                ErrorCode.NotFound,
                $"Training with id `{trainingId}` not found.");
        }

        if (!training.IsAtVersion(expectedVersion))
        {
            return Result<TrainingDto>.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
        }

        var detailsResult = TrainingDetailsFactory.Create(
            request.Title, request.Description, request.Prerequisites, request.AcquiredSkills, request.Topics);

        var result = await detailsResult.MatchAsync(
            async details => await training.EditAsync(
                details.Title,
                details.Description,
                details.Prerequisites,
                details.AcquiredSkills,
                details.Topics,
                uniquenessTitleChecker,
                cancellationToken),
            Result.FailureAsync);

        return await result.MatchAsync(
            onSuccess: async () =>
            {
                trainingRepository.Update(training);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (UniqueConstraintViolationException)
                {
                    // A concurrent request slipped past the uniqueness pre-check;
                    // the unique index is the authoritative guard, so a lost race
                    // is the same business failure as a detected duplicate.
                    return Result<TrainingDto>.Failure(ErrorCode.DuplicateTitle,
                        "A training with the same title already exists for this trainer.");
                }
                catch (ConcurrencyConflictException)
                {
                    // Same layering for the version: the concurrency token is the
                    // authoritative guard behind the pre-check above.
                    return Result<TrainingDto>.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
                }
                return Result<TrainingDto>.Success(training.ToDto());
            },
            onFailure: Result<TrainingDto>.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<Result<List<TrainingDto>>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var trainings = await trainingRepository.GetByTrainerIdAsync(TrainerId.Create(trainerId), cancellationToken);
        return Result<List<TrainingDto>>.Success(trainings.ToDtos());
    }

    /// <inheritdoc />
    public async Task<List<TrainingDto>> GetByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        // Resolving the caller's string against the closed set of topics happens
        // here, before the domain is reached. A name that matches nothing simply
        // matches no training, exactly as an unknown name did before.
        if (!Topic.TryFromName(topic, out var resolvedTopic))
        {
            return [];
        }

        var spec = new TrainingsByTopicSpecification(resolvedTopic);
        var trainings = await trainingRepository.GetAsync(spec, cancellationToken);
        return trainings.ToDtos();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(trainingId), cancellationToken);
        if (training is null)
        {
            return Result.Failure(
                ErrorCode.NotFound,
                $"Training with id `{trainingId}` not found.");
        }

        trainingRepository.Delete(training);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private const string ConcurrencyMessage =
        "The training was modified by someone else since it was read. Reload it and try again.";
}
