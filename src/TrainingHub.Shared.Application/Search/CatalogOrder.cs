namespace TrainingHub.Shared.Application.Search;

/// <summary>
/// The two orders the offered catalog can be read in — a closed set, not a sort expression.
/// </summary>
/// <remarks>
/// Each member names a total order the index stores and <c>ITrainingSearchQuery</c> applies; a
/// caller picks one, and composes nothing (ADR 0071). The member names are the wire vocabulary:
/// the HTTP boundary refuses anything else by asking this type, exactly as <c>[KnownTopic]</c>
/// asks the domain, so adding an order here is what publishes it.
/// </remarks>
public enum CatalogOrder
{
    /// <summary>Alphabetically by title — what a visitor scans, and the default.</summary>
    Title,

    /// <summary>Newest training first, by the training's own age (ADR 0071).</summary>
    Newest
}
