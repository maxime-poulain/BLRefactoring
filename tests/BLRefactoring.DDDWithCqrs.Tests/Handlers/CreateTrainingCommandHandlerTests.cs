using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public class CreateTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();

    public CreateTrainingCommandHandlerTests()
    {
        _titleChecker
            .Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _trainerRepository.Object, _titleChecker.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessAndCallsSave()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            TrainerId = trainer.Id,
            Title = "Advanced C# Patterns",
            Description = "A deep dive into design patterns",
            Prerequisites = "Basic C# knowledge",
            AcquiredSkills = "Design pattern mastery",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeSuccess();
        _trainingRepository.Verify(
            r => r.SaveAsync(It.IsAny<Training>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            TrainerId = Guid.NewGuid(),
            Title = "Some Training",
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
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            TrainerId = trainer.Id,
            Title = "AB", // too short — domain rejects (min 3)
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
}
