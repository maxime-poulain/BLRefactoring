using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTopics;

/// <summary>
/// The facets of the offered catalog: each topic at least one offered training declares, with its
/// count (ADR 0069).
/// </summary>
/// <remarks>
/// It carried nothing until the counts had to answer the search the visitor had typed (ADR 0080).
/// Now it carries the term, and with it a validator — the line ADR 0046 draws is that a validator
/// guards what a message carries, so a message that starts carrying something starts needing one.
/// <para>
/// It does not carry the shelves. Under a widening filter, counting each topic against the ticked
/// ones would give every facet the size of the whole selection and the figures would stop telling
/// the shelves apart.
/// </para>
/// </remarks>
public sealed class GetCatalogTopicsByTermQuery : IQuery<IReadOnlyList<TopicFacetDto>>
{
    /// <summary>
    /// The term the facets are counted under, or <see langword="null"/> for the whole catalog.
    /// </summary>
    public string? Term { get; init; }
}
