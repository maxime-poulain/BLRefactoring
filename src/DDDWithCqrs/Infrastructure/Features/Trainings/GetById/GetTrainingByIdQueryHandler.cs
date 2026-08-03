using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetById;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainings.GetById;

/// <summary>
/// Reads one training, and only if it belongs to the caller.
/// </summary>
/// <remarks>
/// The ownership clause is part of the query rather than a check after it: a training belonging to
/// somebody else is not found, so nothing is read before being refused and the answer carries no
/// trace of it. The action turns the null into a 404 — not a 403, which would confirm that the
/// identifier names something real.
/// <para>
/// This endpoint is the one read by identifier that survives. It is what the edit form loads, and
/// what the <c>Location</c> of a creation points at, so it could not go the way of the four
/// catalogue reads; scoping it to its owner is what makes keeping it safe.
/// </para>
/// </remarks>
public sealed class GetTrainingByIdQueryHandler(
    TrainingContext trainingContext,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetTrainingByIdQuery, TrainingDto?>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<TrainingDto?> Handle(GetTrainingByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainingId = TrainingId.Create(request.Id);
        var trainerId = TrainerId.Create(currentUserService.TrainerId);

        return await trainingContext.Trainings
            .Where(training => training.Id == trainingId && training.TrainerId == trainerId)
            .Select(TrainingProjections.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
