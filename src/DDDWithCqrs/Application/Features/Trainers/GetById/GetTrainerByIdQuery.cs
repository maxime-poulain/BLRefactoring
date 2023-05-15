using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;

/// <summary>
/// Retrieves a <see cref="TrainerDto"/> by its <see cref="TrainerDto.Id"/>.
/// </summary>
public class GetTrainerByIdQuery : IQuery<TrainerDto?>
{
    public GetTrainerByIdQuery(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; init; }
}

public class GetTrainerByIdQueryHandler : IQueryHandler<GetTrainerByIdQuery, TrainerDto?>
{
    private readonly TrainingContext _trainingContext;

    public GetTrainerByIdQueryHandler(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public Task<TrainerDto?> Handle(GetTrainerByIdQuery request, CancellationToken cancellationToken)
    {
        return _trainingContext.Trainers
            .Select(trainer => new TrainerDto()
            {
                Email = trainer.Email.FullAddress,
                Id = trainer.Id,
                Firstname = trainer.Name.Firstname,
                Lastname = trainer.Name.Lastname,
            }).FirstOrDefaultAsync(trainer => trainer.Id == request.Id, cancellationToken);
    }
}
