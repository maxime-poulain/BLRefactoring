using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Contracts;

/// <summary>
/// The annotation that refuses a status name the domain does not spell.
/// </summary>
/// <remarks>
/// It reads the allowed values off the domain type instead of listing them, which is what makes it
/// worth having and also what makes it worth testing: the reflection is the mechanism, and a
/// mechanism that silently found nothing would accept everything. The fact that
/// <c>Withheld</c> — declared after the two statuses that came before it — is accepted here without
/// this file naming it is the whole claim (ADR 0041, ADR 0055).
/// </remarks>
public sealed class KnownStatusAttributeTests
{
    /// <summary>
    /// Is valid, a name the domain declares, accepts it.
    /// </summary>
    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public void IsValid_ANameTheDomainDeclares_AcceptsIt(string status) =>
        new KnownStatusAttribute(typeof(TrainerStatus)).IsValid(status).Should().BeTrue();

    /// <summary>
    /// Is valid, every training status, accepts them all without naming them.
    /// </summary>
    /// <remarks>
    /// Driven off <c>GetStatuses()</c> rather than from three literals, so a fourth state is
    /// accepted the day it is declared and this test cannot be the thing that forgot.
    /// </remarks>
    [Fact]
    public void IsValid_EveryTrainingStatus_AcceptsThemAllWithoutNamingThem()
    {
        var attribute = new KnownStatusAttribute(typeof(TrainingStatus));

        TrainingStatus.GetStatuses()
            .Select(status => attribute.IsValid(status.Name))
            .Should().AllBeEquivalentTo(true)
            .And.HaveCountGreaterThan(1, "a source of names that answered nothing would accept nothing");
    }

    /// <summary>
    /// Is valid, a name no status answers to, refuses it.
    /// </summary>
    [Fact]
    public void IsValid_ANameNoStatusAnswersTo_RefusesIt() =>
        new KnownStatusAttribute(typeof(TrainerStatus)).IsValid("Banished").Should().BeFalse();

    /// <summary>
    /// Is valid, the right name in the wrong case, refuses it.
    /// </summary>
    /// <remarks>
    /// Ordinal on purpose, and this is the fact that pins it. <c>FromName</c> is ordinal, so an
    /// attribute that accepted <c>suspended</c> would hand the domain a name it refuses by throwing
    /// — turning a <c>400</c> a caller can act on into a <c>500</c> two layers away.
    /// </remarks>
    [Fact]
    public void IsValid_TheRightNameInTheWrongCase_RefusesIt() =>
        new KnownStatusAttribute(typeof(TrainerStatus)).IsValid("suspended").Should().BeFalse();

    /// <summary>
    /// Is valid, nothing to judge, accepts it.
    /// </summary>
    /// <remarks>
    /// A filter nobody set is not a filter that failed, and <c>?status=</c> binds to the empty
    /// string rather than to null — so both have to pass, or an empty query parameter would answer
    /// 400 for asking nothing.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NothingToJudge_AcceptsIt(string? status) =>
        new KnownStatusAttribute(typeof(TrainerStatus)).IsValid(status).Should().BeTrue();

    /// <summary>
    /// Is valid, a type that declares no statuses, says so rather than accepting everything.
    /// </summary>
    /// <remarks>
    /// The failure mode reflection invites. An attribute that quietly found no names would refuse
    /// nothing and look exactly like one that works, on every request, for ever. Throwing is what
    /// makes a mistyped <c>typeof</c> a broken build rather than a hole.
    /// </remarks>
    [Fact]
    public void IsValid_ATypeThatDeclaresNoStatuses_SaysSoRatherThanAcceptingEverything()
    {
        var attribute = new KnownStatusAttribute(typeof(KnownStatusAttributeTests));

        var judging = () => attribute.IsValid("anything");

        judging.Should().Throw<InvalidOperationException>()
            .WithMessage("*GetStatuses*");
    }
}
