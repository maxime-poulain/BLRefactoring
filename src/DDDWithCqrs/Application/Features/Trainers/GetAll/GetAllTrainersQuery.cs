using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;

/// <summary>
/// Retrieves every <see cref="TrainerDto"/>.
/// </summary>
public class GetAllTrainersQuery : IQuery<List<TrainerDto>>
{
}

public class GetAllTrainersQueryHandler : IQueryHandler<GetAllTrainersQuery, List<TrainerDto>>
{
    private readonly TrainingContext _trainingContext;

    public GetAllTrainersQueryHandler(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async ValueTask<List<TrainerDto>> Handle(GetAllTrainersQuery request, CancellationToken cancellationToken)
    {
        return await _trainingContext.Trainers.Select(trainer => new TrainerDto()
        {
            Email = trainer.Email.FullAddress,
            Id = trainer.Id,
            Firstname = trainer.Name.Firstname,
            Lastname = trainer.Name.Lastname,
        }).ToListAsync(cancellationToken);
    }
}
