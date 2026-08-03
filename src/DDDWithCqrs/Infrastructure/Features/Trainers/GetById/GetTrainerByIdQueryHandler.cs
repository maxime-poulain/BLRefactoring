using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainers.GetById;

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
