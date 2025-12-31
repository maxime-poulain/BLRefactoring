using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

/// <summary>
/// Retrieves a <see cref="TrainerDto"/> by its <see cref="TrainerDto.Id"/>.
/// </summary>
public class GetTrainerByIdQuery(Guid id) : IQuery<TrainerDto?>
{
    public Guid Id { get; init; } = id;
}

public class GetTrainerByIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainerByIdQuery, TrainerDto?>
{
    public async ValueTask<TrainerDto?> Handle(GetTrainerByIdQuery request, CancellationToken cancellationToken)
    {
        return await trainingContext.Trainers
            .Select(trainer => new TrainerDto()
            {
                Email = trainer.Email.FullAddress,
                Id = trainer.Id,
                Firstname = trainer.Name.Firstname,
                Lastname = trainer.Name.Lastname,
            }).FirstOrDefaultAsync(trainer => trainer.Id == request.Id, cancellationToken);
    }
}
