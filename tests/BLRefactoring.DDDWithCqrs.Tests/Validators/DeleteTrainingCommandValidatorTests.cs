using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

public sealed class DeleteTrainingCommandValidatorTests
{
    private readonly DeleteTrainingCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var command = new DeleteTrainingCommand(Guid.NewGuid());

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyId_HasError()
    {
        var command = new DeleteTrainingCommand(Guid.Empty);

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }
}
