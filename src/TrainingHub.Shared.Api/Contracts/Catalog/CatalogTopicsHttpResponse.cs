namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// The catalog's facets, as <c>GET /Catalog/topics</c> publishes them.
/// </summary>
/// <remarks>
/// A document rather than a bare array, for the rule every list here obeys: a collection leaves in
/// an envelope that can grow, or it cannot grow at all. This one is bounded by the domain — there
/// are never more facets than the closed set holds — so it carries no page, and the envelope is
/// what leaves room for whatever a browse experience learns to need (ADR 0069).
/// </remarks>
public sealed class CatalogTopicsHttpResponse
{
    /// <summary>The facets, alphabetically by topic; empty when nothing is on offer.</summary>
    public required List<CatalogTopicHttpResponse> Topics { get; init; }
}
