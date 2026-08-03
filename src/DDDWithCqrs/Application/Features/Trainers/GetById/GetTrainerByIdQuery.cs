using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;

/// <summary>
/// Asks for a trainer by id.
/// </summary>
public sealed class GetTrainerByIdQuery(Guid id) : IQuery<TrainerDto?>
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public Guid Id { get; init; } = id;
}
