using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Create;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>CreateTrainingCommandHandler</c>.
/// </summary>
public sealed class CreateTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();

    /// <summary>
    /// Create training command handler tests.
    /// </summary>
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

    // Answers zero unless a test raises it: an empty catalogue is the default, so only the
    // test about the capacity rule mentions the counter.
    private readonly Mock<ITrainingCounter> _trainingCounter = new();

    // Answers "not suspended" unless a test raises it, for the same reason as the counter above.
    private readonly Mock<ITrainerStanding> _trainerStanding = new();

    private CreateTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _trainerRepository.Object, _titleChecker.Object, _trainingCounter.Object,
            _trainerStanding.Object, _currentUserService.Object, _unitOfWork.Object);

    /// <summary>
    /// Handle, valid command, returns success and calls save.
    /// </summary>
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

    /// <summary>
    /// Handle, non existing trainer, returns not found failure.
    /// </summary>
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

    /// <summary>
    /// Handle, the catalogue is full, surfaces the domain's refusal and does not save.
    /// </summary>
    /// <remarks>
    /// The rule and its message are the aggregate's, proven in the domain suite; what this
    /// stack owes is to hand the factory the counter and to let the refusal through untouched.
    /// </remarks>
    [Fact]
    public async Task Handle_CatalogueFull_SurfacesTheRefusalAndDoesNotSave()
    {
        var trainer = new TrainerBuilder().Build();
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currentUserService.Setup(s => s.TrainerId).Returns(trainer.Id.Value);
        _currentUserService.Setup(s => s.UserId).Returns(Guid.NewGuid());
        _trainingCounter
            .Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Training.MaximumPerTrainer);
        var sut = CreateSut();

        var command = new CreateTrainingCommand
        {
            Title = "An Eleventh Training",
            Description = "A perfectly valid description",
            Prerequisites = "Perfectly valid prerequisites",
            AcquiredSkills = "Perfectly valid skills",
            Topics = ["Programming"]
        };

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.CatalogueFull);
        _trainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, invalid domain data, returns failure and does not save.
    /// </summary>
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
