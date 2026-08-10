using TrainingHub.Shared.Common;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Factories;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.DDD.Application.Services.TrainerServices;

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

    /// <summary>
    /// Places a trainer under sanction, with a stated reason (ADR 0052).
    /// </summary>
    /// <remarks>
    /// The trainer is named rather than resolved from the caller, which is what separates this from
    /// every other write on this service: those act on whoever is signed in, and this one acts on
    /// somebody else. Who is entitled to do that is settled at the API boundary and nowhere else;
    /// this layer never asks, and could not name the authority that answers without depending on
    /// the API to do so (ADR 0051).
    /// </remarks>
    /// <param name="trainerId">The trainer being sanctioned.</param>
    /// <param name="reason">Why, as the caller wrote it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>Success, why the reason was refused, or why the trainer could not be suspended.</returns>
    Task<Result> SuspendAsync(Guid trainerId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifts a trainer's sanction (ADR 0050).
    /// </summary>
    /// <param name="trainerId">The trainer coming back.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>Success, or a refusal when the trainer was not under sanction.</returns>
    Task<Result> ReinstateAsync(Guid trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of trainers as the administration sees them, newest first (ADR 0055).
    /// </summary>
    /// <remarks>
    /// The one read on this service that is not about the caller and not about one named trainer.
    /// It reads across every trainer, which this API withdrew when it deleted <c>/Trainer/all</c>
    /// and ADR 0055 gives back to a single role — enforced at the boundary, as always, and never
    /// asked about here.
    /// <para>
    /// The status arrives already resolved. Turning a name into a <c>TrainerStatus</c> is the
    /// boundary's job, because the boundary is where an unknown name can be answered with the
    /// parameter that carried it; by the time it reaches this method there is nothing left to
    /// refuse, which is why this one answers a page rather than a <c>Result</c>.
    /// </para>
    /// </remarks>
    /// <param name="request">The standing and the term to narrow by.</param>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, empty when nothing matches.</returns>
    Task<PagedResult<AdministrationTrainerDto>> GetAdministeredPageAsync(
        AdministrationTrainerRequest request,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the trainer use cases: turns raw input into value objects, drives the aggregate, and
/// commits through the unit of work.
/// </summary>
public sealed class TrainerApplicationService(
    ITrainerRepository trainerRepository,
    ITrainerPhotoStore photoStore,
    IPhotoSanitizer photoSanitizer,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
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

        // Three steps, and the order is the whole of ADR 0063. The upload is judged first, because
        // two of the aggregate's rules are about it and sanitization would answer both away — a
        // mismatch re-encoded into the declared format stops being one, and an oversized photograph
        // comes back within the bound. Then the bytes are stripped. Then what is recorded is
        // described from what will actually be stored.
        return await TrainerPhoto
            .Vet(content, contentType)
            .MatchAsync(
                async vetted => await photoSanitizer
                    .Sanitize(content, vetted)
                    .MatchAsync(
                        async sanitized => await TrainerPhoto
                            .Create(
                                sanitized.Content.Span,
                                sanitized.ContentType,
                                timeProvider.GetUtcNow().UtcDateTime)
                            .MatchAsync(
                                async photo => await PublishPhotoAsync(
                                    trainer, photo, sanitized.Content.ToArray(), cancellationToken),
                                Result<TrainerDto>.FailureAsync),
                        Result<TrainerDto>.FailureAsync),
                Result<TrainerDto>.FailureAsync);
    }

    /// <summary>
    /// Writes the bytes, then the row that names them, then drops what the row displaced.
    /// </summary>
    /// <remarks>
    /// Storage is not transactional with the database, so one of the two has to go first and the
    /// choice decides which failure is possible at all. Every crash point below leaves an orphaned
    /// object, which is collectable; none leaves a profile pointing at bytes that are gone.
    /// </remarks>
    private async Task<Result<TrainerDto>> PublishPhotoAsync(
        Trainer trainer,
        TrainerPhoto photo,
        byte[] content,
        CancellationToken cancellationToken)
    {
        // New bytes first, under a key nothing names yet…
        await photoStore.StoreAsync(trainer.Id, photo, content, cancellationToken);

        // …then the row that names them. What it displaces is read before the change rather than
        // returned by it: the aggregate answers whether a change was allowed, nothing more.
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

        // …and only now what it displaced.
        if (replaced is not null)
        {
            await photoStore.DeleteAsync(trainer.Id, replaced, cancellationToken);
        }

        return Result<TrainerDto>.Success(trainer.ToDto());
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

        var stored = await photoStore.FetchAsync(trainer.Id, trainer.Photo.PhotoId, cancellationToken);

        // A row naming bytes the store does not hold should not happen, since writes go in the
        // order that prevents it. Answering "no photo" beats an error nobody can act on.
        return stored is null
            ? null
            : new TrainerPhotoDto(trainer.Photo.PhotoId.Value, stored.Content, stored.ContentType);
    }

    private const string PhotoConcurrencyMessage =
        "The photo was changed by another request while this one was in flight. Try again.";

    /// <inheritdoc />
    public async Task<Result> SuspendAsync(Guid trainerId, string reason, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(trainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Trainer with id `{trainerId}` could not be found.");
        }

        // The reason is judged before the trainer is asked anything: a sanction whose motive the
        // domain refuses must leave no trace, and the aggregate takes a value object precisely so
        // that this layer cannot hand it a sentence nobody vetted.
        var reasonResult = SuspensionReason.Create(reason);

        return await reasonResult.MatchAsync<Result>(
            onSuccess: async suspensionReason => await trainer.Suspend(suspensionReason).MatchAsync(
                onSuccess: async () =>
                {
                    trainerRepository.Update(trainer);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return Result.Success();
                },
                onFailure: Result.FailureAsync),
            onFailure: Result.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<Result> ReinstateAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var trainer = await trainerRepository.GetByIdAsync(TrainerId.Create(trainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"Trainer with id `{trainerId}` could not be found.");
        }

        return await trainer.Reinstate().MatchAsync(
            onSuccess: async () =>
            {
                trainerRepository.Update(trainer);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdministrationTrainerDto>> GetAdministeredPageAsync(
        AdministrationTrainerRequest request,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The repository answers a page of aggregates and this layer maps it — the layered stack's
        // half of ADR 0029, where the CQRS host projects columns instead. Map carries the counts and
        // the position across untouched, which is what keeps the total and the items describing the
        // same set once the mapping is done.
        var page = await trainerRepository.GetPageAsync(
            request.Status, request.Search, paging, cancellationToken);

        return page.Map(trainer => trainer.ToAdministrationDto());
    }
}
