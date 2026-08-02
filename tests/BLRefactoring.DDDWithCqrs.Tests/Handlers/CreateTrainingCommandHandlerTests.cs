using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public sealed class CreateTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();

    public CreateTrainingCommandHandlerTests()
    {
        _titleChecker
            .Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private CreateTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _trainerRepository.Object, _titleChecker.Object, _currentUserService.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessAndCallsSave()
    {
        var trainer = new TrainerBuilder().Build();
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currentUserService.Setup(s => s.TrainerId).Returns(trainer.Id.Value);
        _currentUserService.Setup(s => s.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            Title = "Advanced C# Patterns",
            Description = "A deep dive into design patterns",
            Prerequisites = "Basic C# knowledge",
            AcquiredSkills = "Design pattern mastery",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeSuccess();
        _trainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        var trainerId = Guid.NewGuid();
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _currentUserService.Setup(s => s.TrainerId).Returns(trainerId);
        _currentUserService.Setup(s => s.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            Title = "Some Training",
            Description = "Description",
            Prerequisites = "Prerequisites",
            AcquiredSkills = "Skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _trainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidDomainData_ReturnsFailureAndDoesNotSave()
    {
        var trainer = new TrainerBuilder().Build();
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currentUserService.Setup(s => s.TrainerId).Returns(trainer.Id.Value);
        _currentUserService.Setup(s => s.UserId).Returns(Guid.NewGuid());
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            Title = "AB", // too short — domain rejects (min 3)
            Description = "A valid description",
            Prerequisites = "Valid prerequisites",
            AcquiredSkills = "Valid skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeFailure();
        _trainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
