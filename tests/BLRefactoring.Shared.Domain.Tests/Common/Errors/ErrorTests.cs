using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common.Errors;

/// <summary>
/// Behaviour covered for <c>Error</c>.
/// </summary>
public sealed class ErrorTests
{
    /// <summary>
    /// Constructor, sets properties.
    /// </summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
        var error = new Error(ErrorCodes.Unspecified, "test error");

        error.ErrorCode.Should().Be(ErrorCodes.Unspecified);
        error.ErrorMessage.Should().Be("test error");
    }

    /// <summary>
    /// Equals, same code and message, returns true.
    /// </summary>
    [Fact]
    public void Equals_SameCodeAndMessage_ReturnsTrue()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "test error");
        var error2 = new Error(ErrorCodes.Unspecified, "test error");

        error1.Equals(error2).Should().BeTrue();
    }

    /// <summary>
    /// Equals, different code, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentCode_ReturnsFalse()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "test error");
        var error2 = new Error(ErrorCodes.NotFound, "test error");

        error1.Equals(error2).Should().BeFalse();
    }

    /// <summary>
    /// Equals, different message, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentMessage_ReturnsFalse()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "error 1");
        var error2 = new Error(ErrorCodes.Unspecified, "error 2");

        error1.Equals(error2).Should().BeFalse();
    }

    /// <summary>
    /// To string, returns formatted string.
    /// </summary>
    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var error = new Error(ErrorCodes.Unspecified, "test error");

        error.ToString().Should().Be("Unspecified: test error");
    }

    /// <summary>
    /// Get hash code, same code and message, returns same hash.
    /// </summary>
    [Fact]
    public void GetHashCode_SameCodeAndMessage_ReturnsSameHash()
    {
        var error1 = new Error(ErrorCodes.Unspecified, "test error");
        var error2 = new Error(ErrorCodes.Unspecified, "test error");

        error1.GetHashCode().Should().Be(error2.GetHashCode());
    }
}
