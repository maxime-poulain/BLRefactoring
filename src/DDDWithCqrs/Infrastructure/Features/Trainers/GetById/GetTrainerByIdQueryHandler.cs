using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainers.GetById;

/// <summary>
/// Answers <see cref="GetTrainerByIdQuery"/>.
/// </summary>
public sealed class GetTrainerByIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainerByIdQuery, TrainerDto?>
{
    /// <summary>
    /// Answers the query.
    /// </summary>
    public async ValueTask<TrainerDto?> Handle(GetTrainerByIdQuery request, CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(request.Id);

        return await trainingContext.Trainers
            .Where(trainer => trainer.Id == trainerId)
            .Select(TrainerProjections.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
