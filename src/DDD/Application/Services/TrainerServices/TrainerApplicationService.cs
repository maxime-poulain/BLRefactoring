using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.DDD.Application.Services.TrainerServices;

// A good alternative would have been to have one application service per use case.
// This would have allowed us to have a more granular control over the dependencies.
// Also it makes easier to understand what the underlying class does.
// Example: `ITrainerCreator` or `ITrainerCreationService` is more meaningful
// than `ITrainerApplicationService`.

public interface ITrainerApplicationService
{
    // Another possibility would have been to return just the Id of the newly created Trainer.
    Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request, CancellationToken cancellationToken = default);
    Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class TrainerApplicationService(
    ILogger<TrainerApplicationService> logger,
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ITrainerApplicationService
{
    public async Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request, CancellationToken cancellationToken = default)
    {
        var message = new TrainerCreationMessage
        {
            TrainerId = TrainerId.Generate(),
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            Email = request.Email,
            UserId = UserId.Create(request.UserId),
            Bio = request.Bio
        };

        var result = Trainer.Create(message);

        return await result.MatchAsync(async trainer =>
        {
            trainerRepository.Add(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TrainerDto>.Success(trainer.ToDto());
        }, Result<TrainerDto>.FailureAsync);
    }

    public async Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCode.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        return Result<TrainerDto>.Success(trainer.ToDto());
    }

    public async Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var trainers = await trainerRepository.GetAllAsync(cancellationToken);
        return trainers.Select(x => x.ToDto()).ToArray();
    }

    // This operation works across two different aggregates (Trainer and its Trainings).
    // When the Trainer is marked for deletion, a TrainerDeletedDomainEvent is added to it.
    // The event is dispatched by the infrastructure while the unit of work saves,
    // right before persistence: its handler stages the deletion of the trainer's
    // trainings in the same change tracker, so the trainer and its trainings are
    // deleted atomically, within the single implicit transaction of SaveChangesAsync.
    // No explicit transaction management is required.
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Trainer with id `{id}` not found.");
        }

        try
        {
            trainer.MarkForDeletion();
            trainerRepository.Delete(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while deleting the trainer with id `{TrainerId}`.", id);
            return Result.Failure(ErrorCode.Unspecified, "An error occurred while deleting the trainer.");
        }
        return Result.Success();
    }
}
