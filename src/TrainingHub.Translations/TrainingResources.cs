namespace TrainingHub.Translations;

/// <summary>
/// Marker for the trainer's own training surface — the list, the editor, and the unpublish
/// conversation.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;TrainingResources&gt;</c> uses
/// the type to find the resource files sitting beside it, one per language. See ADR 0089.
/// </remarks>
public sealed class TrainingResources;
