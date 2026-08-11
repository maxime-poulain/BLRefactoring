using AwesomeAssertions;
using TrainingHub.Blazor.Client.Pages.Catalog;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalog;

/// <summary>
/// The dictionary from a topic's name to the shelf hue it wears (ADR 0069).
/// </summary>
/// <remarks>
/// Closed on both sides: the topics are the domain's closed set, and the hues are the custom
/// properties app.css declares. What is worth pinning is the seam between the two — every admitted
/// topic answers its own hue, and an unknown name answers the neutral tone rather than an
/// undefined variable a browser would silently paint as nothing.
/// </remarks>
public sealed class CatalogTopicsTests
{
    /// <summary>
    /// Spine var, an admitted topic, answers its own hue.
    /// </summary>
    [Theory]
    [InlineData("Programming", "var(--th-spine-programming)")]
    [InlineData("Design", "var(--th-spine-design)")]
    [InlineData("Marketing", "var(--th-spine-marketing)")]
    [InlineData("Business", "var(--th-spine-business)")]
    [InlineData("Personal Development", "var(--th-spine-personal-development)")]
    [InlineData("Leadership", "var(--th-spine-leadership)")]
    public void SpineVar_AnAdmittedTopic_AnswersItsOwnHue(string topic, string expected) =>
        CatalogTopics.SpineVar(topic).Should().Be(expected);

    /// <summary>
    /// Spine var, a name the set does not admit, answers the neutral tone.
    /// </summary>
    /// <remarks>
    /// The visible fallback rather than the invisible one: painting an undefined custom property
    /// renders nothing at all, which looks like a defect nobody can reproduce from the markup.
    /// </remarks>
    [Fact]
    public void SpineVar_ANameTheSetDoesNotAdmit_AnswersTheNeutralTone() =>
        CatalogTopics.SpineVar("Alchemy").Should().Be("var(--th-spine-neutral)");
}
