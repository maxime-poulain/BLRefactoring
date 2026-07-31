using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Validators;

public class GetTrainingsByTrainerIdQueryValidatorTests
{
    private readonly GetTrainingsByTrainerIdQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidQuery_IsValid()
    {
        var query = new GetTrainingsByTrainerIdQuery(Guid.NewGuid());

        var result = await _sut.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyTrainerId_HasError()
    {
        var query = new GetTrainingsByTrainerIdQuery(Guid.Empty);

        var result = await _sut.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TrainerId");
    }
}
