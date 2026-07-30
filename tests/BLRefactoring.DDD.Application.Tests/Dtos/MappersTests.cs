using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Dtos;

public class MappersTests
{
    [Fact]
    public void TrainerToDto_MapsIdCorrectly()
    {
        var trainer = new TrainerBuilder().BuildValid();
        var dto = trainer.ToDto();
        dto.Id.Should().Be(trainer.Id.Value);
    }

    [Fact]
    public void TrainerToDto_MapsEmailCorrectly()
    {
        var trainer = new TrainerBuilder().WithEmail("test@example.com").BuildValid();
        var dto = trainer.ToDto();
        dto.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void TrainerToDto_MapsFirstnameCorrectly()
    {
        var trainer = new TrainerBuilder().WithFirstname("Alice").BuildValid();
        var dto = trainer.ToDto();
        dto.Firstname.Should().Be("Alice");
    }

    [Fact]
    public void TrainerToDto_MapsLastnameCorrectly()
    {
        var trainer = new TrainerBuilder().WithLastname("Smith").BuildValid();
        var dto = trainer.ToDto();
        dto.Lastname.Should().Be("Smith");
    }

    [Fact]
    public async Task TrainingToDto_MapsIdCorrectly()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var dto = training.ToDto();
        dto.Id.Should().Be(training.Id.Value);
    }

    [Fact]
    public async Task TrainingToDto_MapsTitleCorrectly()
    {
        var training = await new TrainingBuilder().WithTitle("My Training Title").BuildValidAsync();
        var dto = training.ToDto();
        dto.Title.Should().Be("My Training Title");
    }

    [Fact]
    public async Task TrainingToDto_MapsTrainerIdCorrectly()
    {
        var trainerId = Guid.NewGuid();
        var training = await new TrainingBuilder().WithTrainerId(trainerId).BuildValidAsync();
        var dto = training.ToDto();
        dto.TrainerId.Should().Be(trainerId);
    }

    [Fact]
    public async Task TrainingToDto_MapsTopicsCorrectly()
    {
        var training = await new TrainingBuilder().WithTopics("Programming", "Design").BuildValidAsync();
        var dto = training.ToDto();
        dto.Topics.Should().BeEquivalentTo(["Programming", "Design"]);
    }

    [Fact]
    public async Task TrainingToDto_MapsDescriptionCorrectly()
    {
        var training = await new TrainingBuilder().WithDescription("Test description content").BuildValidAsync();
        var dto = training.ToDto();
        dto.Description.Should().Be("Test description content");
    }

    [Fact]
    public async Task TrainingToDto_MapsPrerequisitesCorrectly()
    {
        var training = await new TrainingBuilder().WithPrerequisites("Basic knowledge needed").BuildValidAsync();
        var dto = training.ToDto();
        dto.Prerequisites.Should().Be("Basic knowledge needed");
    }

    [Fact]
    public async Task TrainingToDto_MapsAcquiredSkillsCorrectly()
    {
        var training = await new TrainingBuilder().WithAcquiredSkills("Advanced patterns skill").BuildValidAsync();
        var dto = training.ToDto();
        dto.AcquiredSkills.Should().Be("Advanced patterns skill");
    }

    [Fact]
    public async Task TrainingsToDtos_MapsAllTrainings()
    {
        var t1 = await new TrainingBuilder().WithTitle("Training One Title").BuildValidAsync();
        var t2 = await new TrainingBuilder().WithTitle("Training Two Title").BuildValidAsync();

        var dtos = new[] { t1, t2 }.ToDtos();

        dtos.Should().HaveCount(2);
    }

    [Fact]
    public void TrainingsToDtos_EmptyCollection_ReturnsEmptyList()
    {
        var dtos = Array.Empty<BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Training>().ToDtos();
        dtos.Should().BeEmpty();
    }
}
