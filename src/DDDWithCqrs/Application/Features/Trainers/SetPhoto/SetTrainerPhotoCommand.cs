using TrainingHub.Shared;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.SetPhoto;

/// <summary>
/// Publishes a photo on the calling trainer's profile, replacing any they already had.
/// </summary>
/// <remarks>
/// One command for both, because there is nothing to tell apart: the caller has bytes and wants
/// them to be their portrait, and whether one was there before is the server's business.
/// </remarks>
public sealed class SetTrainerPhotoCommand : ICommand<Result>
{
    /// <summary>
    /// The uploaded bytes.
    /// </summary>
    public byte[] Content { get; init; } = [];

    /// <summary>
    /// The media type the caller claims the bytes have. Checked against them, never trusted.
    /// </summary>
    public string? ContentType { get; init; }
}

/// <summary>
/// Handles <see cref="SetTrainerPhotoCommand"/>.
/// </summary>
public sealed class SetTrainerPhotoCommandHandler(
    ITrainerRepository trainerRepository,
    ITrainerPhotoStore photoStore,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetTrainerPhotoCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(SetTrainerPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainerId = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(trainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Trainer with id `{trainerId}` could not be found.");
        }

        // The rules about what counts as a photo belong to the aggregate, not to a validator: they
        // are read off the bytes, and the pipeline's validation code means "rejected before the
        // domain saw it", which is the opposite of what happened here.
        var photoResult = TrainerPhoto.Create(request.Content, request.ContentType);

        return await photoResult.MatchAsync<Result>(
            onSuccess: async photo => await PublishAsync(trainer, photo, request.Content, cancellationToken),
            onFailure: Result.FailureAsync);
    }

    private async ValueTask<Result> PublishAsync(
        Trainer trainer,
        TrainerPhoto photo,
        byte[] content,
        CancellationToken cancellationToken)
    {
        // The ordering below is the whole point, and it is the reason there is no Replace on the
        // object store. Storage is not transactional with the database, so one of the two has to
        // go first and the choice decides which failure is possible.
        //
        //   1. Write the new bytes, under a key nothing names yet.
        await photoStore.StoreAsync(trainer.Id, photo, content, cancellationToken);

        //   2. Commit the row that names them. What it displaces is read off the aggregate before
        //      the change, not handed back by it: an aggregate says whether a change was allowed,
        //      it is not a way of reading through to the data.
        var replaced = trainer.Photo;
        trainer.AttachPhoto(photo);
        trainerRepository.Update(trainer);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Two uploads raced and this one lost. The bytes written at step 1 are now unreferenced
            // — rubbish, which is collectable — rather than a profile pointing at nothing.
            return Result.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
        }

        //   3. Only now drop what was displaced. A crash before this line leaves an orphan; a
        //      crash after step 1 leaves an orphan. Neither leaves a row naming absent bytes,
        //      which is the failure the reverse order would make possible.
        if (replaced is not null)
        {
            await photoStore.DeleteAsync(trainer.Id, replaced, cancellationToken);
        }

        return Result.Success();
    }

    private const string ConcurrencyMessage =
        "The photo was changed by another request while this one was in flight. Try again.";
}
