using AwesomeAssertions;
using TrainingHub.Shared.Api.Http;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Http;

/// <summary>
/// Behavior covered for <c>EntityTag</c>.
/// </summary>
public sealed class EntityTagTests
{
    /// <summary>
    /// From, quotes the encoded version.
    /// </summary>
    [Fact]
    public void From_QuotesTheEncodedVersion()
    {
        var tag = EntityTag.From([1, 2, 3, 4]);

        tag.Should().StartWith("\"").And.EndWith("\"");
    }

    /// <summary>
    /// From, then try parse, round trips the version.
    /// </summary>
    [Fact]
    public void From_ThenTryParse_RoundTripsTheVersion()
    {
        var version = new byte[] { 0, 0, 0, 0, 0, 0, 42, 7 };

        EntityTag.TryParse(EntityTag.From(version), out var parsed).Should().BeTrue();

        parsed.Should().Equal(version);
    }

    /// <summary>
    /// Try parse, tolerates surrounding whitespace.
    /// </summary>
    [Fact]
    public void TryParse_ToleratesSurroundingWhitespace()
    {
        var version = new byte[] { 9, 9 };

        EntityTag.TryParse($"  {EntityTag.From(version)} ", out var parsed).Should().BeTrue();

        parsed.Should().Equal(version);
    }

    /// <summary>
    /// Try parse, rejects anything but a strong tag.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    [InlineData("AAAAAAAAB9E=")]
    [InlineData("\"not base 64 at all!\"")]
    [InlineData("\"\"")]
    public void TryParse_RejectsAnythingButAStrongTag(string? candidate)
    {
        // A wildcard or a weak validator would let a caller through without
        // stating which version they read, which is the whole point of the check.
        EntityTag.TryParse(candidate, out var parsed).Should().BeFalse();

        parsed.Should().BeEmpty();
    }
}
