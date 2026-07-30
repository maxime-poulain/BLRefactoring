using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

public class GetTrainerByIdQuery(Guid id) : IQuery<TrainerDto?>
{
    public Guid Id { get; init; } = id;
}
