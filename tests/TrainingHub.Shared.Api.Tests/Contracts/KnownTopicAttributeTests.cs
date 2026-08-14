using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Contracts;

/// <summary>
/// The annotation that refuses a topic name the domain does not spell.
/// </summary>
/// <remarks>
/// <c>KnownStatusAttributeTests</c>'s sibling, and it had none of its own until the filter it
/// judges started arriving as several names at once (ADR 0080). That is the case worth the file:
/// a validation attribute asked about a type it does not judge answers <c>true</c> by
/// framework convention, so an attribute that had not learned collections would have gone on
/// passing every list — including one naming a topic that does not exist — while looking exactly
/// as correct as before. The refusal of the whole boundary would have disappeared in silence.
/// </remarks>
public sealed class KnownTopicAttributeTests
{
    private static KnownTopicAttribute Attribute => new(typeof(Topic));

    /// <summary>
    /// Is valid, every topic the domain declares, accepts them all without naming them.
    /// </summary>
    /// <remarks>
    /// Driven off <c>GetTopics()</c> rather than from sixteen literals, so a seventeenth subject is
    /// accepted the day it is declared and this test cannot be the thing that forgot (ADR 0041).
    /// </remarks>
    [Fact]
    public void IsValid_EveryTopicTheDomainDeclares_AcceptsThemAllWithoutNamingThem() =>
        Topic.GetTopics()
            .Select(topic => Attribute.IsValid(topic.Name))
            .Should().AllBeEquivalentTo(true)
            .And.HaveCountGreaterThan(1, "a source of names that answered nothing would accept nothing");

    /// <summary>
    /// Is valid, a name no topic answers to, refuses it.
    /// </summary>
    [Fact]
    public void IsValid_ANameNoTopicAnswersTo_RefusesIt() =>
        Attribute.IsValid("Astrology").Should().BeFalse();

    /// <summary>
    /// Is valid, the right name in the wrong case, refuses it.
    /// </summary>
    /// <remarks>
    /// Ordinal on purpose, because <c>Topic.TryFromName</c> is ordinal: a name this attribute let
    /// through in the wrong case would reach an index that stores the canonical spelling and match
    /// nothing, which is a filter that silently answers an empty page.
    /// </remarks>
    [Fact]
    public void IsValid_TheRightNameInTheWrongCase_RefusesIt() =>
        Attribute.IsValid("design").Should().BeFalse();

    /// <summary>
    /// Is valid, nothing at all, accepts it.
    /// </summary>
    /// <remarks>
    /// Absence is <c>[Required]</c>'s question, and a blank is what an empty query parameter binds
    /// to: <c>?topic=</c> asks for no filter rather than for a topic called nothing.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NothingAtAll_AcceptsIt(string? nothing) =>
        Attribute.IsValid(nothing).Should().BeTrue();

    /// <summary>
    /// Is valid, several names the domain declares, accepts them.
    /// </summary>
    [Fact]
    public void IsValid_SeveralNamesTheDomainDeclares_AcceptsThem() =>
        Attribute.IsValid(new[] { Topic.Design.Name, Topic.Programming.Name }).Should().BeTrue();

    /// <summary>
    /// Is valid, a list carrying one name no topic answers to, refuses the whole list.
    /// </summary>
    /// <remarks>
    /// <b>The fact this file exists for.</b> Before the attribute learned collections it answered
    /// <c>true</c> here — not because the names were admitted but because a list is not a string,
    /// and the arm that catches everything else passes by convention. The day the filter became
    /// several names, that convention would have turned the boundary's refusal off without changing
    /// a line of it.
    /// </remarks>
    [Fact]
    public void IsValid_AListCarryingOneNameNoTopicAnswersTo_RefusesTheWholeList() =>
        Attribute.IsValid(new[] { Topic.Design.Name, "Astrology" }).Should().BeFalse();

    /// <summary>
    /// Is valid, an empty list, accepts it.
    /// </summary>
    /// <remarks>
    /// A visitor who has ticked nothing is asking for the whole catalog. Same reading as the blank
    /// string above, one shape further out.
    /// </remarks>
    [Fact]
    public void IsValid_AnEmptyList_AcceptsIt() =>
        Attribute.IsValid(Array.Empty<string>()).Should().BeTrue();

    /// <summary>
    /// Is valid, a list carrying a blank, accepts it.
    /// </summary>
    /// <remarks>
    /// <c>?topic=Design&amp;topic=</c> is what a browser sends when a chip is cleared without the
    /// parameter being dropped. The blank is no filter rather than a topic called nothing, exactly
    /// as it is on its own — the normalization that removes it happens one layer in, and this
    /// attribute refusing it would turn a harmless address into a 400.
    /// </remarks>
    [Fact]
    public void IsValid_AListCarryingABlank_AcceptsIt() =>
        Attribute.IsValid(new[] { Topic.Design.Name, "  " }).Should().BeTrue();
}
