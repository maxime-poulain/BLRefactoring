using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;

/// <summary>
/// Retrieves every <see cref="TrainerDto"/>.
/// </summary>
public class GetAllTrainersQuery : IQuery<List<TrainerDto>>
{
}

public class GetAllTrainersQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainersQuery, List<TrainerDto>>
{
    public async ValueTask<List<TrainerDto>> Handle(GetAllTrainersQuery request, CancellationToken cancellationToken)
    {
        return await trainingContext.Trainers.Select(trainer => new TrainerDto()
        {
            Email = trainer.Email.FullAddress,
            Id = trainer.Id,
            Firstname = trainer.Name.Firstname,
            Lastname = trainer.Name.Lastname,
        }).ToListAsync(cancellationToken);
    }
}
