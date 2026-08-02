using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Application.Factories;

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
    /// Retrieves one of the calling trainer's trainings by its identifier.
    /// </summary>
    /// <remarks>
    /// A training belonging to another trainer is reported as not found. The identifier is the
    /// caller's to supply, so this read is scoped rather than argument-free — but it answers only
    /// about what they own.
    /// </remarks>
    Task<Result<TrainingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits an existing training identified by <paramref name="trainingId"/>.
    /// </summary>
    Task<Result<TrainingDto>> EditAsync(Guid trainingId, TrainingEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all trainings belonging to the calling trainer.
    /// </summary>
    /// <remarks>
    /// It takes no trainer: it resolves the caller itself, exactly as <see cref="CreateAsync"/> does
    /// for the owner of a new training. There used to be a sibling reading whichever trainer the
    /// caller named; it went with the endpoint in front of it, and having no parameter is what makes
    /// "only your own" true here rather than a convention.
    /// </remarks>
    Task<List<TrainingDto>> GetMineAsync(CancellationToken cancellationToken = default);

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

        // A training belonging to somebody else is reported as not found, not as forbidden: a 403
        // would confirm that the identifier names something real. Both cases produce the same
        // sentence for the same reason.
        if (training is null || training.TrainerId != TrainerId.Create(currentUserService.TrainerId))
        {
            return Result<TrainingDto>.Failure(ErrorCodes.NotFound, $"Training with id `{id}` not found.");
        }

        return Result<TrainingDto>.Success(training.ToDto());
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
