using TrainingHub.Shared;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.RemovePhoto;

/// <summary>
/// Takes the calling trainer's photo down.
/// </summary>
public sealed class RemoveTrainerPhotoCommand : ICommand<Result>;

/// <summary>
/// Handles <see cref="RemoveTrainerPhotoCommand"/>.
/// </summary>
public sealed class RemoveTrainerPhotoCommandHandler(
    ITrainerRepository trainerRepository,
    ITrainerPhotoStore photoStore,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTrainerPhotoCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(RemoveTrainerPhotoCommand request, CancellationToken cancellationToken)
    {
        var trainerId = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(trainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Trainer with id `{trainerId}` could not be found.");
        }

        var removed = trainer.Photo;

        if (removed is null)
        {
            // Nothing to take down. Said as a failure rather than a silent success so the endpoint
            // can answer 404 for a photo that is not there, which is what a caller deleting one
            // twice deserves to be told. The kernel's code, because the thing this request named —
            // the photo — is what could not be found.
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
            return Result.Failure(ErrorCodes.ConcurrencyConflict,
                "The photo was changed by another request while this one was in flight. Try again.");
        }

        // Same ordering as publishing one, for the same reason: the row stops naming the bytes
        // before the bytes go. A failure here leaves an orphan, never a broken reference.
        await photoStore.DeleteAsync(trainer.Id, removed, cancellationToken);

        return Result.Success();
    }
}
