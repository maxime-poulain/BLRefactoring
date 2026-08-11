namespace TrainingHub.Blazor.Client.Pages.Catalog;

/// <summary>
/// The one translation from a topic's name to the shelf hue it wears.
/// </summary>
/// <remarks>
/// A topic is a shelf, and its hue is the same everywhere it appears (ADR 0069): on the catalog's
/// facet chips, on the spines of a filtered shelf, and on the segments of a training's own sheet.
/// The dictionary is closed because the set it names is: one arm per topic the domain admits, each
/// answering the custom property <c>app.css</c> declares — and the neutral tone for anything else,
/// because an unknown name deserves a visible fallback rather than an undefined variable a browser
/// silently paints as nothing. A new topic costs one arm here and one declaration in the sheet.
/// </remarks>
public static class CatalogTopics
{
    /// <summary>The CSS custom property carrying the topic's shelf hue.</summary>
    public static string SpineVar(string topic) =>
        topic switch
        {
            "Programming" => "var(--th-spine-programming)",
            "Design" => "var(--th-spine-design)",
            "Marketing" => "var(--th-spine-marketing)",
            "Business" => "var(--th-spine-business)",
            "Personal Development" => "var(--th-spine-personal-development)",
            "Leadership" => "var(--th-spine-leadership)",
            _ => "var(--th-spine-neutral)"
        };
}
