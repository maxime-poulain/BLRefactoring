namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// One facet of the offered catalog: a topic, and how many trainings are on offer under it.
/// </summary>
/// <remarks>
/// The first word of the announced Catalog Discovery context spoken by running code (ADR 0069). A
/// facet is not a <c>Topic</c>: the domain's value object says what a training may declare, this
/// shape says what a visitor may browse — a name to filter the search by, and a count that promises
/// the shelf is not empty. Topics no offered training declares are absent rather than zero, because
/// a facet is a way into the catalog and an empty shelf leads nowhere.
/// </remarks>
public sealed class TopicFacetDto
{
    /// <summary>The topic's canonical name, as the domain spells it.</summary>
    public required string Topic { get; init; }

    /// <summary>How many offered trainings are filed under it.</summary>
    public required int OfferedCount { get; init; }
}
