using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

public class EditTrainerCommandValidatorTests
{
    private readonly EditTrainerCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var result = await _sut.ValidateAsync(Command());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyTrainerId_HasError()
    {
        var command = Command(trainerId: Guid.Empty);

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TrainerId");
    }

    [Fact]
    public async Task Validate_EmptyContactEmail_HasError()
    {
        var result = await _sut.ValidateAsync(Command(contactEmail: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public async Task Validate_EmptyFirstname_HasError()
    {
        var result = await _sut.ValidateAsync(Command(firstname: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Firstname");
    }

    [Fact]
    public async Task Validate_EmptyLastname_HasError()
    {
        var result = await _sut.ValidateAsync(Command(lastname: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Lastname");
    }

    [Fact]
    public async Task Validate_NullBio_IsValid()
    {
        // Clearing the bio is a legitimate edition, not a validation error.
        var result = await _sut.ValidateAsync(Command(bio: null));

        result.IsValid.Should().BeTrue();
    }

    private static EditTrainerCommand Command(
        Guid? trainerId = null,
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer.")
        => new()
        {
            TrainerId = trainerId ?? Guid.NewGuid(),
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };
}
