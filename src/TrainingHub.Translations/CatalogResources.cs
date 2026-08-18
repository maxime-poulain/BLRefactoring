namespace TrainingHub.Translations;

/// <summary>
/// Marker for the catalog surface's words — the shelf, a training's page, a trainer's page, and
/// the contact conversation they share.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;CatalogResources&gt;</c> uses
/// the type to find the resource files sitting beside it, one per language. See ADR 0089.
/// </remarks>
public sealed class CatalogResources;
