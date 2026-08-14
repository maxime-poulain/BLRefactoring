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
/// <para>
/// The spelling is what this suite holds, one row per topic. That the rows cover the domain's set
/// at all is a different question, and one this project cannot ask — it references the browser and
/// not the domain. <c>EveryTopicTheDomainDeclares_OwnsAShelfHue</c> asks it instead, from the suite
/// that can see both.
/// </para>
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
    [InlineData("Software Architecture", "var(--th-spine-software-architecture)")]
    [InlineData("Cloud Computing", "var(--th-spine-cloud-computing)")]
    [InlineData("DevOps", "var(--th-spine-devops)")]
    [InlineData("Databases", "var(--th-spine-databases)")]
    [InlineData("Security", "var(--th-spine-security)")]
    [InlineData("Web Development", "var(--th-spine-web-development)")]
    [InlineData("Data and Analytics", "var(--th-spine-data-and-analytics)")]
    [InlineData("Testing and Quality", "var(--th-spine-testing-and-quality)")]
    [InlineData("Project Management", "var(--th-spine-project-management)")]
    [InlineData("Agile Practices", "var(--th-spine-agile-practices)")]
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
