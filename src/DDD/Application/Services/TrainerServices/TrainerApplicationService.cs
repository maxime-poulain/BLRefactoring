using BLRefactoring.Shared.Common;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

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

    /// <summary>
    /// Replaces the profile of the given trainer with the state described by
    /// <paramref name="request"/>, provided nobody else changed it since
    /// <paramref name="expectedVersion"/> was read.
    /// </summary>
    Task<Result<TrainerDto>> EditAsync(Guid id, TrainerEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default);

    Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class TrainerApplicationService(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ITrainerApplicationService
{
    public async Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request, CancellationToken cancellationToken = default)
    {
        var profileResult = TrainerProfileFactory.Create(
            request.Firstname, request.Lastname, request.ContactEmail, request.Bio);

        return await profileResult.MatchAsync(async profile =>
        {
            var trainer = Trainer.Create(
                TrainerId.Generate(),
                UserId.Create(request.UserId),
                profile.Name,
                profile.ContactEmail,
                profile.Bio);

            trainerRepository.Add(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TrainerDto>.Success(trainer.ToDto());
        }, Result<TrainerDto>.FailureAsync);
    }

    public async Task<Result<TrainerDto>> EditAsync(Guid id, TrainerEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCode.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        if (!trainer.IsAtVersion(expectedVersion))
        {
            return Result<TrainerDto>.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
        }

        var profileResult = TrainerProfileFactory.Create(
            request.Firstname, request.Lastname, request.ContactEmail, request.Bio);

        return await profileResult.MatchAsync(async profile =>
        {
            // The aggregate raises one domain event per attribute that actually
            // changed; their handlers run during SaveChangesAsync, before persistence.
            trainer.Edit(profile.Name, profile.ContactEmail, profile.Bio);

            trainerRepository.Update(trainer);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent request slipped past the version pre-check; the
                // concurrency token is the authoritative guard, so a lost race is
                // the same business failure as a detected stale version.
                return Result<TrainerDto>.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
            }

            return Result<TrainerDto>.Success(trainer.ToDto());
        }, Result<TrainerDto>.FailureAsync);
    }

    private const string ConcurrencyMessage =
        "The trainer was modified by someone else since it was read. Reload it and try again.";

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
}
