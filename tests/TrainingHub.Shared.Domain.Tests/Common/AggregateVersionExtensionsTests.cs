using AwesomeAssertions;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common;

/// <summary>
/// Behavior covered for <c>AggregateVersionExtensions</c>.
/// </summary>
public sealed class AggregateVersionExtensionsTests
{
    /// <summary>
    /// Is at version, matching version, returns true.
    /// </summary>
    [Fact]
    public void IsAtVersion_MatchingVersion_ReturnsTrue()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.IsAtVersion(trainer.RowVersion).Should().BeTrue();
    }

    /// <summary>
    /// Is at version, different version, returns false.
    /// </summary>
    [Fact]
    public void IsAtVersion_DifferentVersion_ReturnsFalse()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.IsAtVersion([1, 2, 3, 4, 5, 6, 7, 8]).Should().BeFalse();
    }

    /// <summary>
    /// Is at version, compares content rather than reference.
    /// </summary>
    [Fact]
    public async Task IsAtVersion_ComparesContentRatherThanReference()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        // A version read back from an ETag is a fresh array; only its bytes matter.
        training.IsAtVersion([.. training.RowVersion]).Should().BeTrue();
    }

    /// <summary>
    /// Is at version, null version, throws argument null exception.
    /// </summary>
    [Fact]
    public void IsAtVersion_NullVersion_ThrowsArgumentNullException()
    {
        var trainer = new TrainerBuilder().Build();

        var act = () => trainer.IsAtVersion(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
