using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Edit;
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
/// Behaviour covered for <c>EditTrainingCommandHandler</c>.
/// </summary>
public sealed class EditTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();

    /// <summary>
    /// Edit training command handler tests.
    /// </summary>
    public EditTrainingCommandHandlerTests()
    {
        _titleChecker
            .Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EditTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _titleChecker.Object, _unitOfWork.Object);

    /// <summary>
    /// Handle, valid command, returns success and calls save.
    /// </summary>
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
        _trainingRepository.Verify(r => r.Update(training), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, non existing training, returns not found failure.
    /// </summary>
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

        result.ShouldContainError(ErrorCodes.NotFound);
        _trainingRepository.Verify(r => r.Update(It.IsAny<Training>()), Times.Never);
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
        _trainingRepository.Verify(r => r.Update(It.IsAny<Training>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, duplicate title, returns failure.
    /// </summary>
    [Fact]
    public async Task Handle_DuplicateTitle_ReturnsFailure()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _titleChecker
            .Setup(c => c.TitleForTrainerExistsAsync(
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
        _trainingRepository.Verify(r => r.Update(It.IsAny<Training>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
