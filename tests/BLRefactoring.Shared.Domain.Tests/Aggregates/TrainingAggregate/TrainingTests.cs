using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate;

public class TrainingTests
{
    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ValidData_ReturnsSuccess()
    {
        // Act
        var result = await new TrainingBuilder().BuildAsync();

        // Assert
        result.ShouldBeSuccess();
    }

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
        training.Title.Should().NotBeNull();
        training.Description.Should().NotBeNull();
        training.Prerequisites.Should().NotBeNull();
        training.AcquiredSkills.Should().NotBeNull();
        (training.TrainerId.Value).Should().Be(trainerId);
    }

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

    [Fact]
    public async Task CreateAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChecker = new Mock<IUniquenessTitleChecker>();

        // Act
        var act = () => Training.CreateAsync(null!, mockChecker.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_NullTitleChecker_ThrowsArgumentNullException()
    {
        // Arrange
        var message = new TrainingCreationMessage
        {
            TrainingId = TrainingId.Generate(),
            Title = "Valid Title",
            Description = "Valid description",
            Prerequisites = "Valid prerequisites",
            AcquiredSkills = "Valid acquired skills",
            Topics = ["Programming"],
            TrainerId = TrainerId.Generate()
        };

        // Act
        var act = () => Training.CreateAsync(message, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidTitle_ReturnsFailure()
    {
        // Act
        var result = await new TrainingBuilder()
            .WithTitle("ab")
            .BuildAsync();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task CreateAsync_InvalidDescription_ReturnsFailure()
    {
        // Act
        var result = await new TrainingBuilder()
            .WithDescription("")
            .BuildAsync();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task CreateAsync_MultipleInvalidFields_ReturnsAllErrors()
    {
        // Act
        var result = await new TrainingBuilder()
            .WithTitle("ab")
            .WithDescription("")
            .WithPrerequisites("")
            .WithAcquiredSkills("")
            .BuildAsync();

        // Assert
        var errors = result.ShouldBeFailure();
        errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }

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

    [Fact]
    public async Task CreateAsync_InvalidTopic_ThrowsArgumentException()
    {
        // Act
        var act = () => new TrainingBuilder()
            .WithTopics("NonExistentTopic")
            .BuildAsync();

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // --- EditAsync ---

    [Fact]
    public async Task EditAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        var training = await new TrainingBuilder().BuildValidAsync();
        var mockChecker = new Mock<IUniquenessTitleChecker>();
        mockChecker.Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var message = new TrainingEditionMessage
        {
            Title = "A Different Valid Title",
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Marketing"]
        };

        // Act
        var result = await training.EditAsync(message, mockChecker.Object);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task EditAsync_SameTitle_DoesNotCheckUniqueness()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();

        var mockChecker = new Mock<IUniquenessTitleChecker>();
        mockChecker.Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var message = new TrainingEditionMessage
        {
            Title = "Valid Training Title",
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Programming"]
        };

        // Act
        await training.EditAsync(message, mockChecker.Object);

        // Assert
        mockChecker.Verify(
            c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditAsync_DifferentTitle_ChecksUniqueness()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();

        var mockChecker = new Mock<IUniquenessTitleChecker>();
        mockChecker.Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var message = new TrainingEditionMessage
        {
            Title = "A Completely Different Title",
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Programming"]
        };

        // Act
        await training.EditAsync(message, mockChecker.Object);

        // Assert
        mockChecker.Verify(
            c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_DuplicateTitle_ReturnsFailure()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();

        var mockChecker = new Mock<IUniquenessTitleChecker>();
        mockChecker.Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var message = new TrainingEditionMessage
        {
            Title = "A Completely Different Title",
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Programming"]
        };

        // Act
        var result = await training.EditAsync(message, mockChecker.Object);

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public async Task EditAsync_InvalidFields_DoNotMutateAggregate()
    {
        // Arrange
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .BuildValidAsync();

        var originalTitle = training.Title;

        var mockChecker = new Mock<IUniquenessTitleChecker>();

        var message = new TrainingEditionMessage
        {
            Title = "ab",
            Description = "",
            Prerequisites = "",
            AcquiredSkills = "",
            Topics = ["Programming"]
        };

        // Act
        var result = await training.EditAsync(message, mockChecker.Object);

        // Assert
        result.ShouldBeFailure();
        training.Title.Should().Be(originalTitle);
    }
}
