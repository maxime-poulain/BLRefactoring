using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetPhoto;

/// <summary>
/// Asks for a trainer's photo by the trainer's identifier.
/// </summary>
/// <param name="trainerId">The trainer whose photo is wanted.</param>
/// <remarks>
/// By identifier rather than "mine", unlike the commands beside it. Publishing a portrait is
/// self-service; looking at one is what a catalog of trainers does, and this query was shaped
/// for the day that catalog became public — which arrived with ADR 0062, through the catalog's
/// own anonymous portrait routes beside this authenticated one.
/// </remarks>
public sealed class GetTrainerPhotoByTrainerIdQuery(Guid trainerId) : IQuery<TrainerPhotoDto?>
{
    /// <summary>
    /// The trainer whose photo is wanted.
    /// </summary>
    public Guid TrainerId { get; init; } = trainerId;
}
