using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetByTopic;

/// <remarks>
/// The caller's string is resolved against the closed set of topics before it reaches the database,
/// which is what the layered stack has always done — and what this handler did not.
/// <para>
/// Sending it straight to SQL looked equivalent and was not: the comparison then happens under the
/// database's collation, which is case- and trailing-space-insensitive by default. So
/// <c>by-topic/programming</c> returned trainings on this host and nothing on the other, from the
/// same data, through an API whose whole claim is that the two hosts answer alike. The topic
/// vocabulary belongs to the domain; a database collation is not entitled to widen it.
/// </para>
/// </remarks>
public class GetTrainingsByTopicQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTopicQuery, PagedResult<TrainingDto>>
{
    public async ValueTask<PagedResult<TrainingDto>> Handle(
        GetTrainingsByTopicQuery request,
        CancellationToken cancellationToken)
    {
        if (!Topic.TryFromName(request.Topic, out var topic))
        {
            // A name that matches no topic matches no training. An empty page rather than a 404:
            // the collection exists, it is the filter that selects nothing.
            return new PagedResult<TrainingDto>([], request.Page, request.PageSize, TotalCount: 0);
        }

        var topicName = topic.Name;

        return await trainingContext.Trainings
            .Where(training => training.Topics.Any(t => t.Name == topicName))
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(TrainingProjections.ToDtoExpression, request, cancellationToken);
    }
}
