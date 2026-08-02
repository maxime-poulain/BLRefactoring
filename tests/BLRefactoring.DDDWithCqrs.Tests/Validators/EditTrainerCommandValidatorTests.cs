using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

/// <summary>
/// Behaviour covered for <c>EditTrainerCommandValidator</c>.
/// </summary>
public sealed class EditTrainerCommandValidatorTests
{
    private readonly EditTrainerCommandValidator _sut = new();

    /// <summary>
    /// Validate, valid command, is valid.
    /// </summary>
    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var result = await _sut.ValidateAsync(Command());

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Validate, empty contact email, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyContactEmail_HasError()
    {
        var result = await _sut.ValidateAsync(Command(contactEmail: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }

    /// <summary>
    /// Validate, empty firstname, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyFirstname_HasError()
    {
        var result = await _sut.ValidateAsync(Command(firstname: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
    }

    /// <summary>
    /// Validate, empty lastname, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyLastname_HasError()
    {
        var result = await _sut.ValidateAsync(Command(lastname: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Lastname");
    }

    /// <summary>
    /// Validate, null bio, is valid.
    /// </summary>
    [Fact]
    public async Task Validate_NullBio_IsValid()
    {
        // Clearing the bio is a legitimate edition, not a validation error.
        var result = await _sut.ValidateAsync(Command(bio: null));

        result.IsValid.Should().BeTrue();
    }

    private static EditTrainerCommand Command(
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer.")
        => new()
        {
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };
}
