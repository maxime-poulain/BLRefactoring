using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

public class CreateTrainingCommandValidatorTests
{
    private readonly CreateTrainingCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var command = new CreateTrainingCommand
        {

            Title = "Advanced C# Patterns",
            Topics = ["Programming"],
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyTopics_HasError()
    {
        var command = new CreateTrainingCommand
        {

            Title = "Valid Title",
            Topics = [],
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Topics");
    }
}
