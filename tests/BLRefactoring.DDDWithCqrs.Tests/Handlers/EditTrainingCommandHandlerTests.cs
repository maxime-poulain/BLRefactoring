using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public class EditTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();

    public EditTrainingCommandHandlerTests()
    {
        _titleChecker
            .Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private EditTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _titleChecker.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessAndCallsSave()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = CreateSut();

        var command = new EditTrainingCommand
        {
            TrainingId = training.Id.Value,
            Title = "Updated Training Title",
            Description = "Updated description",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeSuccess();
        _trainingRepository.Verify(
            r => r.SaveAsync(It.IsAny<Training>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = CreateSut();

        var command = new EditTrainingCommand
        {
            TrainingId = Guid.NewGuid(),
            Title = "Some Title",
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldContainError(ErrorCode.NotFound);
        _trainingRepository.Verify(
            r => r.SaveAsync(It.IsAny<Training>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidDomainData_ReturnsFailureAndDoesNotSave()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = CreateSut();

        var command = new EditTrainingCommand
        {
            TrainingId = training.Id.Value,
            Title = "AB", // too short - domain rejects (min 3)
            Description = "A valid description",
            Prerequisites = "Valid prerequisites",
            AcquiredSkills = "Valid skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeFailure();
        _trainingRepository.Verify(
            r => r.SaveAsync(It.IsAny<Training>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateTitle_ReturnsFailure()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _titleChecker
            .Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();

        var command = new EditTrainingCommand
        {
            TrainingId = training.Id.Value,
            Title = "A Different Title",
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeFailure();
        _trainingRepository.Verify(
            r => r.SaveAsync(It.IsAny<Training>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
