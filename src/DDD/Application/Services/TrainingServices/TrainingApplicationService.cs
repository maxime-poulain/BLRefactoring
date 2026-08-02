using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Projections;
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
    Task<List<TrainingDto>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all trainings belonging to the calling trainer.
    /// </summary>
    /// <remarks>
    /// The same read as <see cref="GetByTrainerIdAsync"/> and deliberately a separate method, and it
    /// takes no trainer at all: this one resolves the caller itself, exactly as
    /// <see cref="CreateAsync"/> already does for the owner of a new training. One method takes a
    /// trainer the caller names; the other serves a trainer the caller cannot choose, and having no
    /// parameter is what makes that true rather than a convention.
    /// </remarks>
    Task<List<TrainingDto>> GetMineAsync(CancellationToken cancellationToken = default);

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
public sealed class TrainingApplicationService(
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
        var trainerId = TrainerId.Create(currentUserService.TrainerId);

        if (!await trainerRepository.ExistsAsync(trainerId, cancellationToken))
        {
            return Result<TrainingDto>.Failure(
                ErrorCodes.NotFound,
                $"Trainer with id `{trainerId.Value}` not found.");
        }

        var detailsResult = TrainingDetailsFactory.Create(
            request.Title, request.Description, request.Prerequisites, request.AcquiredSkills, request.Topics);

        var result = await detailsResult.MatchAsync(
            async details => await Training.CreateAsync(
                TrainingId.Generate(),
                trainerId,
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
                return Result<TrainingDto>.Failure(TrainingErrorCodes.DuplicateTitle,
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
            ? Result<TrainingDto>.Failure(ErrorCodes.NotFound, $"Training with id `{id}` not found.")
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
                ErrorCodes.NotFound,
                $"Training with id `{trainingId}` not found.");
        }

        if (!training.IsAtVersion(expectedVersion))
        {
            return Result<TrainingDto>.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
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
                    return Result<TrainingDto>.Failure(TrainingErrorCodes.DuplicateTitle,
                        "A training with the same title already exists for this trainer.");
                }
                catch (ConcurrencyConflictException)
                {
                    // Same layering for the version: the concurrency token is the
                    // authoritative guard behind the pre-check above.
                    return Result<TrainingDto>.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
                }
                return Result<TrainingDto>.Success(training.ToDto());
            },
            onFailure: Result<TrainingDto>.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<List<TrainingDto>> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        // A plain list, not a Result. Nothing here can fail: the identifier is already a Guid by
        // the time it arrives, and a trainer with no trainings has none rather than being an
        // error. Wrapping it made the controller write a failure branch that could not be taken
        // and declare a 400 no caller could receive.
        var trainings = await trainingRepository.GetByTrainerIdAsync(TrainerId.Create(trainerId), cancellationToken);
        return trainings.ToDtos();
    }

    /// <inheritdoc />
    public async Task<List<TrainingDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        // Resolved here, not received. The same repository call the named-trainer read makes -- two
        // reads answering "which trainings belong to this trainer" must not be able to disagree --
        // and the only thing separating them is that this one cannot be pointed anywhere else.
        var trainerId = TrainerId.Create(currentUserService.TrainerId);

        var trainings = await trainingRepository.GetByTrainerIdAsync(trainerId, cancellationToken);
        return trainings.ToDtos();
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
                ErrorCodes.NotFound,
                $"Training with id `{trainingId}` not found.");
        }

        trainingRepository.Delete(training);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private const string ConcurrencyMessage =
        "The training was modified by someone else since it was read. Reload it and try again.";
}
