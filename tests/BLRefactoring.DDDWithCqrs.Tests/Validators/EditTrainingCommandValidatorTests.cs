using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

/// <summary>
/// Behaviour covered for <c>EditTrainingCommandValidator</c>.
/// </summary>
public sealed class EditTrainingCommandValidatorTests
{
    private readonly EditTrainingCommandValidator _sut = new();

    /// <summary>
    /// Validate, valid command, is valid.
    /// </summary>
    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var command = new EditTrainingCommand
        {
            TrainingId = Guid.NewGuid(),
            Title = "Advanced C# Patterns",
            Topics = ["Programming"],
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Validate, empty training id, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyTrainingId_HasError()
    {
        var command = new EditTrainingCommand
        {
            TrainingId = Guid.Empty,
            Title = "Valid Title",
            Topics = ["Programming"],
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TrainingId");
    }

    /// <summary>
    /// Validate, empty title, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyTitle_HasError()
    {
        var command = new EditTrainingCommand
        {
            TrainingId = Guid.NewGuid(),
            Title = "",
            Topics = ["Programming"],
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills"
        };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    /// <summary>
    /// Validate, empty topics, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyTopics_HasError()
    {
        var command = new EditTrainingCommand
        {
            TrainingId = Guid.NewGuid(),
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
