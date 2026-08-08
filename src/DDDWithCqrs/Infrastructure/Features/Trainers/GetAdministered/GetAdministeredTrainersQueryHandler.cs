using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetAdministered;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainers.GetAdministered;

/// <summary>
/// Projects one page of trainers for the administration, filtered in the database rather than
/// after it.
/// </summary>
/// <remarks>
/// Every <c>Where</c> is composed before <c>ToPagedResultAsync</c>, so the filters reach SQL and the
/// count and the page agree on the same set. Filtering a materialised page instead would answer a
/// short page with a total about every trainer — numbers that still look like numbers, which is why
/// a test pins this and not only the rows.
/// <para>
/// <c>FromName</c> is called on a name two guards have already accepted: the <c>[KnownStatus]</c>
/// annotation at model binding, and the query's own validator in the pipeline. That is what lets it
/// keep throwing rather than answering a result (ADR 0046).
/// </para>
/// <para>
/// It projects columns and touches no specification, which is what ADR 0028 requires of a reader.
/// The criteria here are a status and a term — never an expression handed in from outside, which is
/// the line ADR 0055 draws and a rule defends.
/// </para>
/// </remarks>
public sealed class GetAdministeredTrainersQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAdministeredTrainersQuery, PagedResult<AdministrationTrainerDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<PagedResult<AdministrationTrainerDto>> Handle(
        GetAdministeredTrainersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainers = trainingContext.Trainers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TrainerStatus.FromName(request.Status);
            trainers = trainers.Where(trainer => trainer.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            trainers = trainers.Where(trainer =>
                trainer.Name.Firstname.Contains(term)
                || trainer.Name.Lastname.Contains(term)
                || trainer.ContactEmail.FullAddress.Contains(term));
        }

        return await trainers
            .NewestFirst<Trainer, TrainerId>()
            .ToPagedResultAsync(
                TrainerProjections.ToAdministrationDtoExpression, request.Paging, cancellationToken);
    }
}
