namespace TrainingHub.Translations;

/// <summary>
/// Marker for the shell's own words — the labels and verbs every screen shares.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;CommonResources&gt;</c> uses
/// the type to find the resource files sitting beside it, one per language, and that is its whole
/// job. See ADR 0088.
/// </remarks>
public sealed class CommonResources;
