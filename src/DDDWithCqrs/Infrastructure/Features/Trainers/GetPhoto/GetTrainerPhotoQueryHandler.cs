using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetPhoto;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainers.GetPhoto;

/// <summary>
/// Handles <see cref="GetTrainerPhotoQuery"/>.
/// </summary>
/// <remarks>
/// Two reads, and they cannot be one: the database knows which photo a trainer has, the object
/// store knows what it looks like. The row is loaded rather than projected because the store is
/// asked in terms of the photo itself — that is what keeps the key layout out of everything above
/// the infrastructure.
/// </remarks>
public sealed class GetTrainerPhotoQueryHandler(
    TrainingContext trainingContext,
    ITrainerPhotoStore photoStore)
    : IQueryHandler<GetTrainerPhotoQuery, TrainerPhotoDto?>
{
    /// <inheritdoc />
    public async ValueTask<TrainerPhotoDto?> Handle(
        GetTrainerPhotoQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainerId = TrainerId.Create(request.TrainerId);

        var trainer = await trainingContext.Trainers
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == trainerId, cancellationToken);

        if (trainer?.Photo is null)
        {
            return null;
        }

        var stored = await photoStore.FetchAsync(trainer.Id, trainer.Photo, cancellationToken);

        if (stored is null)
        {
            // The row names bytes the store does not have. That should not happen — writes go in
            // the order that prevents it — but a store is a separate system, and answering "no
            // photo" is better than answering with an error the caller cannot act on.
            return null;
        }

        return new TrainerPhotoDto(trainer.Photo.PhotoId, stored.Content, stored.ContentType);
    }
}
