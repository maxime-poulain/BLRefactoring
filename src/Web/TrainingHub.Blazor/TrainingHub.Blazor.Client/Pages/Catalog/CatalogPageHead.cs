using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace TrainingHub.Blazor.Client.Pages.Catalog;

/// <summary>
/// What the catalog's pages put in the document's head for the machines that read it: the
/// description a search result quotes, and the structured data a rich result is built from
/// (ADR 0073).
/// </summary>
public static class CatalogPageHead
{
    // The length a search engine displays before truncating for itself. Cutting here, at a word
    // boundary, is what keeps the snippet ours rather than wherever the display happened to stop.
    private const int SummaryLength = 160;

    /// <summary>
    /// Collapses a text to a single line of at most ~160 characters, cut at a word boundary, or
    /// answers the fallback when there is no text to summarize.
    /// </summary>
    /// <param name="text">The prose to summarize — a training's description, a trainer's bio.</param>
    /// <param name="fallback">What to say when the prose is missing or blank.</param>
    /// <returns>One line fit for a <c>description</c> meta tag.</returns>
    public static string Summarize(string? text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        // A description is written in paragraphs; a meta tag holds one line. Splitting on any
        // whitespace and joining on one space collapses both the newlines and the double spaces.
        var singleLine = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (singleLine.Length <= SummaryLength)
        {
            return singleLine;
        }

        var boundary = singleLine.LastIndexOf(' ', SummaryLength);

        return $"{singleLine[..(boundary > 0 ? boundary : SummaryLength)]}…";
    }

    /// <summary>
    /// Serializes a JSON-LD document and wraps it in the whole <c>script</c> element, ready to
    /// render as markup.
    /// </summary>
    /// <param name="document">
    /// The document, as a dictionary — the one shape that can spell <c>@context</c> and
    /// <c>@type</c>, since <c>@</c> in a C# property name is only an identifier escape.
    /// </param>
    /// <returns>The element, as markup the head can carry.</returns>
    /// <remarks>
    /// The default serializer encoder is the safety here: it escapes <c>&lt;</c>, <c>&gt;</c> and
    /// quotes to <c>\uXXXX</c>, so a title an author wrote cannot close the script element and
    /// start its own. <c>UnsafeRelaxedJsonEscaping</c> would reopen exactly that hole — do not
    /// trade it in for prettier JSON. The element is built whole because a literal script tag in
    /// a component is refused by the compiler (RZ9992).
    /// </remarks>
    public static MarkupString JsonLdScript(Dictionary<string, object?> document) =>
        new($"""<script type="application/ld+json">{JsonSerializer.Serialize(document)}</script>""");
}
