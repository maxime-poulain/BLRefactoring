using BLRefactoring.Shared.Common;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.DDD.Application.Services.TrainerServices;

// A good alternative would have been to have one application service per use case.
// This would have allowed us to have a more granular control over the dependencies.
// Also it makes easier to understand what the underlying class does.
// Example: `ITrainerCreator` or `ITrainerCreationService` is more meaningful
// than `ITrainerApplicationService`.

/// <summary>
/// The trainer use cases of the layered stack.
/// </summary>
public interface ITrainerApplicationService
{
    // Another possibility would have been to return just the Id of the newly created Trainer.

    /// <summary>
    /// Creates a trainer from raw input.
    /// </summary>
    /// <param name="request">The unvalidated input.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The trainer, or every rule the input broke.</returns>
    Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the profile of the given trainer with the state described by
    /// <paramref name="request"/>, provided nobody else changed it since
    /// <paramref name="expectedVersion"/> was read.
    /// </summary>
    Task<Result<TrainerDto>> EditAsync(TrainerEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one trainer.
    /// </summary>
    /// <param name="id">The trainer's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The trainer, or a not-found failure.</returns>
    Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a photo on the calling trainer's profile, replacing any they already had.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="contentType">The media type the caller claims they have.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated profile, or why the photo was refused.</returns>
    Task<Result<TrainerDto>> SetPhotoAsync(
        byte[] content,
        string? contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the calling trainer's photo down.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why nothing was taken down.</returns>
    Task<Result> RemovePhotoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a trainer's photo.
    /// </summary>
    /// <param name="id">The trainer whose photo is wanted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The photo, or <see langword="null"/> when there is none.</returns>
    Task<TrainerPhotoDto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the trainer use cases: turns raw input into value objects, drives the aggregate, and
/// commits through the unit of work.
/// </summary>
public sealed class TrainerApplicationService(
    ITrainerRepository trainerRepository,
    ITrainerPhotoStore photoStore,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ITrainerApplicationService
{
    /// <summary>
    /// Creates a trainer from raw input, or returns every rule it broke.
    /// </summary>
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

    /// <summary>
    /// Replaces a trainer's profile, refusing the write when the expected version is stale.
    /// </summary>
    public async Task<Result<TrainerDto>> EditAsync(TrainerEditionRequest request, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        // Resolved here rather than received. PUT /Trainer/me has never had a trainer for a caller
        // to choose, and taking one as a parameter made that a promise every call site had to keep.
        var id = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCodes.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        if (!trainer.IsAtVersion(expectedVersion))
        {
            return Result<TrainerDto>.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
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
                return Result<TrainerDto>.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
            }

            return Result<TrainerDto>.Success(trainer.ToDto());
        }, Result<TrainerDto>.FailureAsync);
    }

    private const string ConcurrencyMessage =
        "The trainer was modified by someone else since it was read. Reload it and try again.";

    /// <summary>
    /// Reads one trainer, or a not-found failure.
    /// </summary>
    public async Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCodes.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        return Result<TrainerDto>.Success(trainer.ToDto());
    }

    /// <inheritdoc />
    public async Task<Result<TrainerDto>> SetPhotoAsync(
        byte[] content,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var id = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCodes.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        // What counts as a photo is the aggregate's rule, read off the bytes themselves.
        var photoResult = TrainerPhoto.Create(content, contentType);

        return await photoResult.MatchAsync(async photo =>
        {
            // Storage is not transactional with the database, so the order below is what decides
            // which failure is possible at all. New bytes first, under a key nothing names yet…
            await photoStore.StoreAsync(trainer.Id, photo, content, cancellationToken);

            // …then the row that names them. What it displaces is read before the change rather
            // than returned by it: the aggregate answers whether a change was allowed, nothing more.
            var replaced = trainer.Photo;
            trainer.AttachPhoto(photo);
            trainerRepository.Update(trainer);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<TrainerDto>.Failure(ErrorCodes.ConcurrencyConflict, PhotoConcurrencyMessage);
            }

            // …and only now what it displaced. Every crash point above leaves an orphaned object,
            // which is collectable; none leaves a profile pointing at bytes that are gone.
            if (replaced is not null)
            {
                await photoStore.DeleteAsync(trainer.Id, replaced, cancellationToken);
            }

            return Result<TrainerDto>.Success(trainer.ToDto());
        }, Result<TrainerDto>.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<Result> RemovePhotoAsync(CancellationToken cancellationToken = default)
    {
        var id = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Trainer with id `{id}` could not be found.");
        }

        var removed = trainer.Photo;

        if (removed is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "This trainer has no photo.");
        }

        trainer.RemovePhoto();

        trainerRepository.Update(trainer);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(ErrorCodes.ConcurrencyConflict, PhotoConcurrencyMessage);
        }

        await photoStore.DeleteAsync(trainer.Id, removed, cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<TrainerPhotoDto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(id), cancellationToken);

        if (trainer?.Photo is null)
        {
            return null;
        }

        var stored = await photoStore.FetchAsync(trainer.Id, trainer.Photo, cancellationToken);

        // A row naming bytes the store does not hold should not happen, since writes go in the
        // order that prevents it. Answering "no photo" beats an error nobody can act on.
        return stored is null
            ? null
            : new TrainerPhotoDto(trainer.Photo.PhotoId, stored.Content, stored.ContentType);
    }

    private const string PhotoConcurrencyMessage =
        "The photo was changed by another request while this one was in flight. Try again.";
}
