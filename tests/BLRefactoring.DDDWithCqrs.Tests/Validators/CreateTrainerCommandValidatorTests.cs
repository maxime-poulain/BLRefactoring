using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

/// <summary>
/// Behaviour covered for <c>CreateTrainerCommandValidator</c>.
/// </summary>
public sealed class CreateTrainerCommandValidatorTests
{
    private readonly CreateTrainerCommandValidator _sut = new();

    /// <summary>
    /// Validate, valid command, is valid.
    /// </summary>
    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Validate, empty email, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyEmail_HasError()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = ""
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }

    /// <summary>
    /// Validate, empty firstname, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyFirstname_HasError()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "",
            Lastname = "Doe",
            ContactEmail = "john@example.com"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
    }
}
