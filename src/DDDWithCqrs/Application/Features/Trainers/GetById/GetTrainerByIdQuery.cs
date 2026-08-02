using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

public sealed class GetTrainerByIdQuery(Guid id) : IQuery<TrainerDto?>
{
    public Guid Id { get; init; } = id;
}
