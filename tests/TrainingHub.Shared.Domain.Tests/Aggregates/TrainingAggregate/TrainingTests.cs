using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainingAggregate;

/// <summary>
/// The aggregate only accepts value objects, so malformed input cannot reach it:
/// those rules belong to the value objects and are covered by their own tests, and
/// resolving topic names is the application layer's job. The only rule left for the
/// aggregate is the one it cannot settle alone — a title must be unique among the
/// trainings of the same trainer.
/// </summary>
public sealed class TrainingTests
{
    // --- CreateAsync ---

    /// <summary>
    /// Create async, valid data, returns success.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidData_ReturnsSuccess()
    {
        // Act
        var result = await new TrainingBuilder().BuildAsync();

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create async, valid data, sets all properties.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidData_SetsAllProperties()
    {
        // Arrange
        var trainerId = Guid.NewGuid();

        // Act
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithDescription("A valid training description for testing purposes")
            .WithPrerequisites("Basic programming knowledge required")
            .WithAcquiredSkills("Advanced design patterns mastery")
            .WithTrainerId(trainerId)
            .BuildValidAsync();

        // Assert
        training.Title.Value.Should().Be("Valid Training Title");
        training.Description.Value.Should().Be("A valid training description for testing purposes");
        training.Prerequisites.Value.Should().Be("Basic programming knowledge required");
        training.AcquiredSkills.Value.Should().Be("Advanced design patterns mastery");
        training.TrainerId.Value.Should().Be(trainerId);
    }

    /// <summary>
    /// Create async, valid data, sets topics.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidData_SetsTopics()
    {
        // Act
        var training = await new TrainingBuilder()
            .WithTopics("Programming", "Design")
            .BuildValidAsync();

        // Assert
        training.Topics.Should().HaveCount(2);
        training.Topics.Select(t => t.Name).Should().Contain("Programming");
        training.Topics.Select(t => t.Name).Should().Contain("Design");
    }

    /// <summary>
    /// Create async, valid data, raises training created event.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidData_RaisesTrainingCreatedEvent()
    {
        // Act
        var training = await new TrainingBuilder().BuildValidAsync();

        // Assert
        training.DomainEvents.Should().ContainSingle(e => e is TrainingCreatedDomainEvent);
        training.DomainEvents.Should().NotContain(e => e is TrainingEditedDomainEvent);
        var domainEvent = training.DomainEvents.OfType<TrainingCreatedDomainEvent>().Single();
        domainEvent.TrainingId.Should().Be(training.Id);
        domainEvent.TrainerId.Should().Be(training.TrainerId);
    }

    /// <summary>
    /// Create async, null title, throws argument null exception.
    /// </summary>
    [Fact]
    public async Task CreateAsync_NullTitle_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = new Mock<IUniquenessTitleChecker>().Object;

        // Act
        var act = () => Training.CreateAsync(
            TrainingId.Generate(), TrainerId.Generate(),
            null!, Description(), Prerequisites(), Skills(), Topics(), checker);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Create async, null title checker, throws argument null exception.
    /// </summary>
    [Fact]
    public async Task CreateAsync_NullTitleChecker_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Training.CreateAsync(
            TrainingId.Generate(), TrainerId.Generate(),
            Title(), Description(), Prerequisites(), Skills(), Topics(), null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Create async, duplicate title, returns failure.
    /// </summary>
    [Fact]
    public async Task CreateAsync_DuplicateTitle_ReturnsFailure()
    {
        // Act
        var result = await new TrainingBuilder()
            .WithTitleAlreadyExists()
            .BuildAsync();

        // Assert
        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create async, duplicate topics, are deduplicated.
    /// </summary>
    [Fact]
    public async Task CreateAsync_DuplicateTopics_AreDeduplicated()
    {
        // Act
        var training = await new TrainingBuilder()
            .WithTopics("Programming", "Programming", "Design")
            .BuildValidAsync();

        // Assert
        training.Topics.Should().HaveCount(2);
        training.Topics.Should().Contain(Topic.Programming);
        training.Topics.Should().Contain(Topic.Design);
    }

    // --- EditAsync ---

    /// <summary>
    /// Edit async, valid data, returns success and updates properties.
    /// </summary>
    [Fact]
    public async Task EditAsync_ValidData_ReturnsSuccessAndUpdatesProperties()
    {
        // Arrange
        var training = await new TrainingBuilder().BuildValidAsync();
        var checker = AvailableTitleChecker();

        // Act
        var result = await training.EditAsync(
            Title("A Different Valid Title"),
            Description("Updated description for the training"),
            Prerequisites("Updated prerequisites content"),
            Skills("Updated acquired skills content"),
            Topics(Topic.Marketing),
            checker.Object);

        // Assert
        result.ShouldBeSuccess();
        training.Title.Value.Should().Be("A Different Valid Title");
        training.Topics.Should().ContainSingle().Which.Should().Be(Topic.Marketing);
    }

    /// <summary>
    /// Edit async, valid data, raises training edited event.
    /// </summary>
    [Fact]
    public async Task EditAsync_ValidData_RaisesTrainingEditedEvent()
    {
        // Arrange
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();
        var checker = AvailableTitleChecker();

        // Act
        await training.EditAsync(
            Title("A Different Valid Title"), Description(), Prerequisites(), Skills(),
            Topics(Topic.Marketing), checker.Object);

        // Assert
        training.DomainEvents.Should().ContainSingle(e => e is TrainingEditedDomainEvent);
        training.DomainEvents.Should().NotContain(e => e is TrainingCreatedDomainEvent);
        var domainEvent = training.DomainEvents.OfType<TrainingEditedDomainEvent>().Single();
        domainEvent.TrainingId.Should().Be(training.Id);
        domainEvent.TrainerId.Should().Be(training.TrainerId);
    }

    /// <summary>
    /// Edit async, same title, does not check uniqueness.
    /// </summary>
    [Fact]
    public async Task EditAsync_SameTitle_DoesNotCheckUniqueness()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();
        var checker = AvailableTitleChecker();

        // Act
        await training.EditAsync(
            Title("Valid Training Title"), Description(), Prerequisites(), Skills(),
            Topics(), checker.Object);

        // Assert
        checker.Verify(
            c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Edit async, different title, checks uniqueness.
    /// </summary>
    [Fact]
    public async Task EditAsync_DifferentTitle_ChecksUniqueness()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();
        var checker = AvailableTitleChecker();

        // Act
        await training.EditAsync(
            Title("A Completely Different Title"), Description(), Prerequisites(), Skills(),
            Topics(), checker.Object);

        // Assert
        checker.Verify(
            c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Edit async, duplicate title, fails without mutating the aggregate.
    /// </summary>
    [Fact]
    public async Task EditAsync_DuplicateTitle_FailsWithoutMutatingTheAggregate()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();
        var originalDescription = training.Description;

        var checker = new Mock<IUniquenessTitleChecker>();
        checker.Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await training.EditAsync(
            Title("A Completely Different Title"),
            Description("A description that must not be applied"),
            Prerequisites(), Skills(), Topics(), checker.Object);

        // Assert — the rejected title takes the whole edition down with it.
        result.ShouldBeFailure();
        training.Title.Value.Should().Be("Valid Training Title");
        training.Description.Should().Be(originalDescription);
    }

    /// <summary>
    /// Edit async, duplicate topics, are deduplicated.
    /// </summary>
    [Fact]
    public async Task EditAsync_DuplicateTopics_AreDeduplicated()
    {
        // Arrange
        var training = await new TrainingBuilder().BuildValidAsync();
        var checker = AvailableTitleChecker();

        // Act
        await training.EditAsync(
            Title("A Different Valid Title"), Description(), Prerequisites(), Skills(),
            Topics(Topic.Programming, Topic.Programming, Topic.Design), checker.Object);

        // Assert
        training.Topics.Should().HaveCount(2);
    }

    private static Mock<IUniquenessTitleChecker> AvailableTitleChecker()
    {
        var checker = new Mock<IUniquenessTitleChecker>();
        checker.Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return checker;
    }

    private static TrainingTitle Title(string value = "Valid Training Title")
        => TrainingTitle.Create(value).ShouldBeSuccess();

    private static TrainingDescription Description(string value = "A valid training description for testing purposes")
        => TrainingDescription.Create(value).ShouldBeSuccess();

    private static TrainingPrerequisites Prerequisites(string value = "Basic programming knowledge required")
        => TrainingPrerequisites.Create(value).ShouldBeSuccess();

    private static AcquiredSkills Skills(string value = "Advanced design patterns mastery")
        => AcquiredSkills.Create(value).ShouldBeSuccess();

    private static IReadOnlyCollection<Topic> Topics(params Topic[] topics)
        => topics.Length == 0 ? [Topic.Programming] : topics;
}
