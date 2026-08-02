using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

/// <summary>
/// Behaviour covered for <c>DeleteTrainingCommandValidator</c>.
/// </summary>
public sealed class DeleteTrainingCommandValidatorTests
{
    private readonly DeleteTrainingCommandValidator _sut = new();

    /// <summary>
    /// Validate, valid command, is valid.
    /// </summary>
    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var command = new DeleteTrainingCommand(Guid.NewGuid());

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Validate, empty id, has error.
    /// </summary>
    [Fact]
    public async Task Validate_EmptyId_HasError()
    {
        var command = new DeleteTrainingCommand(Guid.Empty);

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }
}
