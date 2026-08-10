namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// One facet of the offered catalog, as <c>GET /Catalog/topics</c> publishes it.
/// </summary>
/// <remarks>
/// A name to browse by and a count that promises the shelf is not empty — the first word of the
/// Catalog Discovery language on the wire (ADR 0069). Topics no offered training declares are
/// absent rather than zero: a facet is a way into the catalog, and an empty shelf leads nowhere.
/// </remarks>
public sealed class CatalogTopicHttpResponse
{
    /// <summary>The topic's canonical name, as the domain spells it.</summary>
    public required string Topic { get; init; }

    /// <summary>How many offered trainings are filed under it.</summary>
    public required int OfferedCount { get; init; }
}
