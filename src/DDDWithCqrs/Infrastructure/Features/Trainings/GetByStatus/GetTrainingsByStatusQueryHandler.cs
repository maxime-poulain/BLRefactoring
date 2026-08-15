using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetByStatus;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainings.GetByStatus;

/// <summary>
/// Projects one page of trainings across every trainer, filtered in the database rather than after
/// it.
/// </summary>
/// <remarks>
/// The sibling reader <c>GetTrainingsByCurrentTrainerQueryHandler</c> opens with a <c>Where</c> on the caller's
/// own identifier, and this one has no such line — deliberately, and it is the only reader here of
/// which that is true. Reading across every trainer is what ADR 0055 gives back to one role, and
/// the absence of that <c>Where</c> is where the decision actually lives.
/// <para>
/// Everything else follows the sibling: the filter composed before <c>ToPagedResultAsync</c> so the
/// count and the page describe the same set, columns projected rather than aggregates loaded, and
/// no specification touched (ADR 0028).
/// </para>
/// <para>
/// Two filters, like the trainers' reader. The second arrived with ADR 0060: a converted title is
/// one EF Core cannot look inside, a complex property is one it can — checked in both directions
/// rather than assumed.
/// </para>
/// <para>
/// The owner's name is read in the same statement, by a correlated sub-select the projection can
/// afford because this reader can see the trainers' table. Its layered counterpart cannot — it maps
/// aggregates, and a <c>Training</c> has never known its owner's name — so it looks the names up
/// for the whole page through <c>ITrainerNamesQuery</c> instead. Two mechanisms, one shape, held
/// together by an end-to-end fact both hosts answer.
/// </para>
/// </remarks>
public sealed class GetTrainingsByStatusQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByStatusQuery, PagedResult<AdministrationTrainingDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<PagedResult<AdministrationTrainingDto>> Handle(
        GetTrainingsByStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainings = trainingContext.Trainings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TrainingStatus.FromName(request.Status);
            trainings = trainings.Where(training => training.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            trainings = trainings.Where(training => training.Title.Value.Contains(term));
        }

        var trainers = trainingContext.Trainers;

        return await trainings
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(
                training => new AdministrationTrainingDto
                {
                    Id = training.Id.Value,
                    TrainerId = training.TrainerId.Value,
                    TrainerName = trainers
                        .Where(trainer => trainer.Id == training.TrainerId)
                        .Select(trainer => trainer.Name.Firstname + " " + trainer.Name.Lastname)
                        .FirstOrDefault(),
                    Title = training.Title.Value,
                    Status = training.Status.Name,
                    WithholdingReason = training.WithholdingReason == null ? null : training.WithholdingReason.Value
                },
                request.Paging,
                cancellationToken);
    }
}
