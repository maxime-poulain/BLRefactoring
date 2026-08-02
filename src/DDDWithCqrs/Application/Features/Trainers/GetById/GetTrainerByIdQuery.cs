using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

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
