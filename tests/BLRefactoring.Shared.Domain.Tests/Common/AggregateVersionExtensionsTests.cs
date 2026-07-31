using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class AggregateVersionExtensionsTests
{
    [Fact]
    public void IsAtVersion_MatchingVersion_ReturnsTrue()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.IsAtVersion(trainer.RowVersion).Should().BeTrue();
    }

    [Fact]
    public void IsAtVersion_DifferentVersion_ReturnsFalse()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.IsAtVersion([1, 2, 3, 4, 5, 6, 7, 8]).Should().BeFalse();
    }

    [Fact]
    public void IsAtVersion_ComparesContentRatherThanReference()
    {
        var training = new TrainingBuilder().BuildValidAsync().GetAwaiter().GetResult();

        // A version read back from an ETag is a fresh array; only its bytes matter.
        training.IsAtVersion([.. training.RowVersion]).Should().BeTrue();
    }

    [Fact]
    public void IsAtVersion_NullVersion_ThrowsArgumentNullException()
    {
        var trainer = new TrainerBuilder().Build();

        var act = () => trainer.IsAtVersion(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
