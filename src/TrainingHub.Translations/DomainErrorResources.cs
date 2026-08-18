namespace TrainingHub.Translations;

/// <summary>
/// Marker for the domain's refusals, keyed by the very codes the aggregates raise —
/// <c>Training.DuplicateTitle</c>, never a sentence.
/// </summary>
/// <remarks>
/// The domain authors codes and stays free of this assembly (ADR 0088 pins both directions); the
/// edge that owns the HTTP answer looks the visitor's sentence up here by code, and falls back to
/// the domain's own English when the catalog has no entry. The neutral entries restate the
/// domain's sentences verbatim, so an English answer reads the same whichever side authored it.
/// </remarks>
public sealed class DomainErrorResources;
