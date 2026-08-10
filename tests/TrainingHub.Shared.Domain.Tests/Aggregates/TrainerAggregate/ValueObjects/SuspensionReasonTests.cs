using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>SuspensionReason</c>.
/// </summary>
public sealed class SuspensionReasonTests
{
    /// <summary>
    /// Create, a written reason, returns success.
    /// </summary>
    [Fact]
    public void Create_AWrittenReason_ReturnsSuccess()
    {
        var reason = SuspensionReason.Create("Repeated breaches of the content policy.")
            .ShouldBeSuccess();

        reason.Value.Should().Be("Repeated breaches of the content policy.");
    }

    /// <summary>
    /// Create, surrounding whitespace, is trimmed before it is measured.
    /// </summary>
    /// <remarks>
    /// The trim is what makes the blank refusal mean what it says: five spaces are refused for
    /// being blank rather than by a length check reaching the same answer down a different path
    /// (ADR 0044).
    /// </remarks>
    [Fact]
    public void Create_SurroundingWhitespace_IsTrimmedBeforeItIsMeasured()
    {
        var reason = SuspensionReason.Create("   Off-topic content.   ").ShouldBeSuccess();

        reason.Value.Should().Be("Off-topic content.");
    }

    /// <summary>
    /// Create, nothing to say, is refused.
    /// </summary>
    /// <remarks>
    /// The rule this value object exists for. A sanction the product cannot explain to the person
    /// it lands on is exactly what ADR 0052 set out to prevent, so blank is a refusal rather than
    /// an empty reason.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NothingToSay_IsRefused(string? value)
    {
        SuspensionReason.Create(value!)
            .ShouldContainError(TrainerErrorCodes.SuspensionReasonEmpty);
    }

    /// <summary>
    /// Create, longer than the field holds, is refused.
    /// </summary>
    [Fact]
    public void Create_LongerThanTheFieldHolds_IsRefused()
    {
        SuspensionReason.Create(new string('a', SuspensionReason.MaximumLength + 1))
            .ShouldContainError(TrainerErrorCodes.SuspensionReasonTooLong);
    }

    /// <summary>
    /// Create, exactly the maximum, is accepted.
    /// </summary>
    /// <remarks>
    /// The boundary asserted from the constant rather than from a literal, so the two cannot drift.
    /// </remarks>
    [Fact]
    public void Create_ExactlyTheMaximum_IsAccepted()
    {
        SuspensionReason.Create(new string('a', SuspensionReason.MaximumLength)).ShouldBeSuccess();
    }

    /// <summary>
    /// Two reasons with the same words, are the same value.
    /// </summary>
    [Fact]
    public void TwoReasonsWithTheSameWords_AreTheSameValue()
    {
        SuspensionReason.Create("Spam.").ShouldBeSuccess()
            .Should().Be(SuspensionReason.Create("Spam.").ShouldBeSuccess());
    }
}
