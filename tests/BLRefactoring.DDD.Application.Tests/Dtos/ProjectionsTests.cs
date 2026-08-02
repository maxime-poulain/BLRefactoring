using AwesomeAssertions;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Dtos;

/// <summary>
/// Behaviour covered for <c>Projections</c>.
/// </summary>
public sealed class ProjectionsTests
{
    /// <summary>
    /// Trainer to dto, maps id correctly.
    /// </summary>
    [Fact]
    public void TrainerToDto_MapsIdCorrectly()
    {
        var trainer = new TrainerBuilder().Build();
        var dto = trainer.ToDto();
        dto.Id.Should().Be(trainer.Id.Value);
    }

    /// <summary>
    /// Trainer to dto, maps contact email correctly.
    /// </summary>
    [Fact]
    public void TrainerToDto_MapsContactEmailCorrectly()
    {
        var trainer = new TrainerBuilder().WithContactEmail("test@example.com").Build();
        var dto = trainer.ToDto();
        dto.ContactEmail.Should().Be("test@example.com");
    }

    /// <summary>
    /// Trainer to dto, maps firstname correctly.
    /// </summary>
    [Fact]
    public void TrainerToDto_MapsFirstnameCorrectly()
    {
        var trainer = new TrainerBuilder().WithFirstname("Alice").Build();
        var dto = trainer.ToDto();
        dto.Firstname.Should().Be("Alice");
    }

    /// <summary>
    /// Trainer to dto, maps lastname correctly.
    /// </summary>
    [Fact]
    public void TrainerToDto_MapsLastnameCorrectly()
    {
        var trainer = new TrainerBuilder().WithLastname("Smith").Build();
        var dto = trainer.ToDto();
        dto.Lastname.Should().Be("Smith");
    }

    /// <summary>
    /// Trainer to dto, maps bio correctly.
    /// </summary>
    [Fact]
    public void TrainerToDto_MapsBioCorrectly()
    {
        var trainer = new TrainerBuilder().WithBio("Trains people for a living.").Build();
        var dto = trainer.ToDto();
        dto.Bio.Should().Be("Trains people for a living.");
    }

    // The absent bio deserves its own case: the projection reads it through a ternary rather than
    // `?.`, which an expression tree forbids, and that ternary is the one branch the compiled
    // mapper did not exercise before both stacks started sharing a single expression.

    /// <summary>
    /// Trainer to dto, without bio, maps to null.
    /// </summary>
    [Fact]
    public void TrainerToDto_WithoutBio_MapsToNull()
    {
        var trainer = new TrainerBuilder().WithoutBio().Build();
        var dto = trainer.ToDto();
        dto.Bio.Should().BeNull();
    }

    /// <summary>
    /// Training to dto, maps id correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsIdCorrectly()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var dto = training.ToDto();
        dto.Id.Should().Be(training.Id.Value);
    }

    /// <summary>
    /// Training to dto, maps title correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsTitleCorrectly()
    {
        var training = await new TrainingBuilder().WithTitle("My Training Title").BuildValidAsync();
        var dto = training.ToDto();
        dto.Title.Should().Be("My Training Title");
    }

    /// <summary>
    /// Training to dto, maps trainer id correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsTrainerIdCorrectly()
    {
        var trainerId = Guid.NewGuid();
        var training = await new TrainingBuilder().WithTrainerId(trainerId).BuildValidAsync();
        var dto = training.ToDto();
        dto.TrainerId.Should().Be(trainerId);
    }

    /// <summary>
    /// Training to dto, maps topics correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsTopicsCorrectly()
    {
        var training = await new TrainingBuilder().WithTopics("Programming", "Design").BuildValidAsync();
        var dto = training.ToDto();
        dto.Topics.Should().BeEquivalentTo(["Programming", "Design"]);
    }

    /// <summary>
    /// Training to dto, maps description correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsDescriptionCorrectly()
    {
        var training = await new TrainingBuilder().WithDescription("Test description content").BuildValidAsync();
        var dto = training.ToDto();
        dto.Description.Should().Be("Test description content");
    }

    /// <summary>
    /// Training to dto, maps prerequisites correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsPrerequisitesCorrectly()
    {
        var training = await new TrainingBuilder().WithPrerequisites("Basic knowledge needed").BuildValidAsync();
        var dto = training.ToDto();
        dto.Prerequisites.Should().Be("Basic knowledge needed");
    }

    /// <summary>
    /// Training to dto, maps acquired skills correctly.
    /// </summary>
    [Fact]
    public async Task TrainingToDto_MapsAcquiredSkillsCorrectly()
    {
        var training = await new TrainingBuilder().WithAcquiredSkills("Advanced patterns skill").BuildValidAsync();
        var dto = training.ToDto();
        dto.AcquiredSkills.Should().Be("Advanced patterns skill");
    }

    /// <summary>
    /// Trainings to dtos, maps all trainings.
    /// </summary>
    [Fact]
    public async Task TrainingsToDtos_MapsAllTrainings()
    {
        var t1 = await new TrainingBuilder().WithTitle("Training One Title").BuildValidAsync();
        var t2 = await new TrainingBuilder().WithTitle("Training Two Title").BuildValidAsync();

        var dtos = new[] { t1, t2 }.ToDtos();

        dtos.Should().HaveCount(2);
    }

    /// <summary>
    /// Trainings to dtos, empty collection, returns empty list.
    /// </summary>
    [Fact]
    public void TrainingsToDtos_EmptyCollection_ReturnsEmptyList()
    {
        var dtos = Array.Empty<BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Training>().ToDtos();
        dtos.Should().BeEmpty();
    }
}
