using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

public sealed class CreateTrainerCommandValidatorTests
{
    private readonly CreateTrainerCommandValidator _sut = new();

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
