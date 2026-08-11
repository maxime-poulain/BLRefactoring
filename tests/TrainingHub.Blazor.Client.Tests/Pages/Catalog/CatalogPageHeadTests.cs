using AwesomeAssertions;
using TrainingHub.Blazor.Client.Pages.Catalog;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalog;

/// <summary>
/// Behavior covered for the head's two builders: the one-line summary a description meta tag
/// carries, and the JSON-LD script element (ADR 0073).
/// </summary>
/// <remarks>
/// Unit tests rather than rendered pages, because the branches worth holding are textual: where a
/// long description is cut, what a missing one falls back to, and whether an author's prose can
/// close the script element it is serialized into. A rendered page shows one path through each;
/// these show all of them.
/// </remarks>
public sealed class CatalogPageHeadTests
{
    private const string Fallback = "A training on offer.";

    /// <summary>
    /// Missing prose, answers the fallback.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n\t ")]
    public void MissingProse_AnswersTheFallback(string? text) =>
        CatalogPageHead.Summarize(text, Fallback).Should().Be(Fallback,
            "a page with nothing to say still owes the crawler a sentence");

    /// <summary>
    /// Short prose, is collapsed to one line and kept whole.
    /// </summary>
    [Fact]
    public void ShortProse_IsCollapsedToOneLineAndKeptWhole() =>
        CatalogPageHead.Summarize("Written in\nparagraphs,  with   doubled spaces.", Fallback)
            .Should().Be("Written in paragraphs, with doubled spaces.",
                "a description is written in paragraphs and a meta tag holds one line");

    /// <summary>
    /// Long prose, is cut at a word boundary.
    /// </summary>
    [Fact]
    public void LongProse_IsCutAtAWordBoundary()
    {
        var prose = string.Join(' ', Enumerable.Repeat("sentence", 30));

        var summary = CatalogPageHead.Summarize(prose, Fallback);

        summary.Should().EndWith("sentence…",
            "the cut lands on a word boundary — a snippet ending mid-word reads as a mistake");
        summary.Length.Should().BeLessThanOrEqualTo(161,
            "the summary stays within what a search result displays, ellipsis included");
    }

    /// <summary>
    /// An unbroken run, is cut at the ceiling.
    /// </summary>
    [Fact]
    public void AnUnbrokenRun_IsCutAtTheCeiling() =>
        CatalogPageHead.Summarize(new string('a', 200), Fallback)
            .Should().Be($"{new string('a', 160)}…",
                "prose with no space to cut at is cut at the length itself rather than kept whole");

    /// <summary>
    /// The script element, is whole, and an authors markup cannot close it.
    /// </summary>
    [Fact]
    public void TheScriptElement_IsWhole_AndAnAuthorsMarkupCannotCloseIt()
    {
        var script = CatalogPageHead.JsonLdScript(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Course",
            ["name"] = "</script><script>alert(1)</script>"
        }).Value;

        script.Should().StartWith("<script type=\"application/ld+json\">");
        script.Should().EndWith("</script>");
        script.Should().Contain("</script>", Exactly.Once(),
            "the default encoder escapes an author's angle brackets, so the one closing tag is ours");
        script.Should().Contain("\"@context\"",
            "the dictionary is the one shape that can spell the vocabulary's own keys");
    }
}
