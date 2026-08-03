using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetById;

/// <summary>
/// Asks for a training by id.
/// </summary>
public sealed class GetTrainingByIdQuery(Guid id) : IQuery<TrainingDto?>
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public Guid Id { get; } = id;
}
