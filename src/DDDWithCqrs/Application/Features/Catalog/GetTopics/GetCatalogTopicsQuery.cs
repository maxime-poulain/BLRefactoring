using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTopics;

/// <summary>
/// The facets of the offered catalog: each topic at least one offered training declares, with its
/// count (ADR 0069).
/// </summary>
/// <remarks>
/// The emptiest query this application carries, and honestly so: a visitor asking what there is to
/// browse has nothing to say yet. No validator, by the line ADR 0046 draws — a validator guards
/// what a message carries, and this one carries nothing.
/// </remarks>
public sealed class GetCatalogTopicsQuery : IQuery<IReadOnlyList<TopicFacetDto>>;
