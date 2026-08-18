namespace TrainingHub.Translations;

/// <summary>
/// Marker for the administration surface's words — the dashboard, the two moderation screens,
/// the outbox, and the reason dialog they share.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;AdministrationResources&gt;</c>
/// uses the type to find the resource files sitting beside it, one per language. See ADR 0089.
/// </remarks>
public sealed class AdministrationResources;
