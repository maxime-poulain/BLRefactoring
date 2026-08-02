using AwesomeAssertions;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Factories;

/// <summary>
/// Turning primitives into value objects is the application layer's job, so this is
/// where the accumulation of validation errors is covered — the aggregate can no
/// longer receive a malformed input at all.
/// </summary>
public sealed class TrainerProfileFactoryTests
{
    /// <summary>
    /// Create, valid input, returns the value objects.
    /// </summary>
    [Fact]
    public void Create_ValidInput_ReturnsTheValueObjects()
    {
        var result = TrainerProfileFactory.Create(
            "John", "Doe", "john.doe@example.com", "An experienced trainer.");

        var profile = result.ShouldBeSuccess();
        profile.Name.Firstname.Should().Be("John");
        profile.Name.Lastname.Should().Be("Doe");
        profile.ContactEmail.FullAddress.Should().Be("john.doe@example.com");
        profile.Bio!.Value.Should().Be("An experienced trainer.");
    }

    /// <summary>
    /// Create, null bio, is valid and yields no bio.
    /// </summary>
    [Fact]
    public void Create_NullBio_IsValidAndYieldsNoBio()
    {
        var result = TrainerProfileFactory.Create("John", "Doe", "john.doe@example.com", bio: null);

        result.ShouldBeSuccess().Bio.Should().BeNull();
    }

    /// <summary>
    /// Create, empty bio, returns failure.
    /// </summary>
    [Fact]
    public void Create_EmptyBio_ReturnsFailure()
    {
        // A provided bio still goes through the value object: "no bio" is null,
        // not an empty string.
        var result = TrainerProfileFactory.Create("John", "Doe", "john.doe@example.com", bio: "");

        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create, invalid contact email, returns failure.
    /// </summary>
    [Fact]
    public void Create_InvalidContactEmail_ReturnsFailure()
    {
        var result = TrainerProfileFactory.Create("John", "Doe", "not-an-email", null);

        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create, invalid name, returns failure.
    /// </summary>
    [Fact]
    public void Create_InvalidName_ReturnsFailure()
    {
        var result = TrainerProfileFactory.Create("J", "Doe", "john.doe@example.com", null);

        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create, several invalid inputs, reports them all at once.
    /// </summary>
    [Fact]
    public void Create_SeveralInvalidInputs_ReportsThemAllAtOnce()
    {
        var result = TrainerProfileFactory.Create("", "", "not-an-email", new string('x', 501));

        var errors = result.ShouldBeFailure();
        errors.Should().HaveCountGreaterThan(2);
    }
}
