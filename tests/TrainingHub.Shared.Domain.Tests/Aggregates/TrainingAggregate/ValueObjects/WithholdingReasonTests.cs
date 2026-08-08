using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Behaviour covered for <c>WithholdingReason</c>.
/// </summary>
public sealed class WithholdingReasonTests
{
    /// <summary>
    /// Create, a written reason, returns success.
    /// </summary>
    [Fact]
    public void Create_AWrittenReason_ReturnsSuccess()
    {
        var reason = WithholdingReason.Create("Uses images the author does not hold rights to.")
            .ShouldBeSuccess();

        reason.Value.Should().Be("Uses images the author does not hold rights to.");
    }

    /// <summary>
    /// Create, surrounding whitespace, is trimmed before it is measured.
    /// </summary>
    [Fact]
    public void Create_SurroundingWhitespace_IsTrimmedBeforeItIsMeasured()
    {
        var reason = WithholdingReason.Create("  Misleading title.  ").ShouldBeSuccess();

        reason.Value.Should().Be("Misleading title.");
    }

    /// <summary>
    /// Create, nothing to say, is refused.
    /// </summary>
    /// <remarks>
    /// A training taken out of public view without a word leaves its owner nothing to act on, which
    /// is the whole thing ADR 0052 asks this value object to carry.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NothingToSay_IsRefused(string? value)
    {
        WithholdingReason.Create(value!)
            .ShouldContainError(TrainingErrorCodes.WithholdingReasonEmpty);
    }

    /// <summary>
    /// Create, longer than the field holds, is refused.
    /// </summary>
    [Fact]
    public void Create_LongerThanTheFieldHolds_IsRefused()
    {
        WithholdingReason.Create(new string('a', WithholdingReason.MaximumLength + 1))
            .ShouldContainError(TrainingErrorCodes.WithholdingReasonTooLong);
    }

    /// <summary>
    /// Create, exactly the maximum, is accepted.
    /// </summary>
    [Fact]
    public void Create_ExactlyTheMaximum_IsAccepted()
    {
        WithholdingReason.Create(new string('a', WithholdingReason.MaximumLength)).ShouldBeSuccess();
    }

    /// <summary>
    /// Two reasons with the same words, are the same value.
    /// </summary>
    [Fact]
    public void TwoReasonsWithTheSameWords_AreTheSameValue()
    {
        WithholdingReason.Create("Off topic.").ShouldBeSuccess()
            .Should().Be(WithholdingReason.Create("Off topic.").ShouldBeSuccess());
    }
}
