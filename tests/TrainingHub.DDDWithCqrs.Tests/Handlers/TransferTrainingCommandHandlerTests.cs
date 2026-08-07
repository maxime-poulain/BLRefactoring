using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Transfer;
using TrainingHub.Shared;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>TransferTrainingCommandHandler</c>.
/// </summary>
/// <remarks>
/// The decision itself is <c>TrainingTransferDomainService</c>'s and is proven in the domain suite;
/// these facts cover the orchestration — the not-found refusal, the ghost recipient refused
/// before the service is consulted, and the save under the unique index (ADR 0036).
/// </remarks>
public sealed class TransferTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ITrainingCounter> _trainingCounter = new();
    private readonly Mock<IUniquenessTitleChecker> _titleChecker = new();
    private readonly Mock<ITrainerStanding> _trainerStanding = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private TransferTrainingCommandHandler CreateSut() => new(
        _trainingRepository.Object,
        _trainerRepository.Object,
        _trainingCounter.Object,
        _titleChecker.Object,
        _trainerStanding.Object,
        _unitOfWork.Object);

    /// <summary>
    /// Handle, an existing training and a willing recipient, transfers and saves.
    /// </summary>
    [Fact]
    public async Task Handle_AnExistingTrainingAndAWillingRecipient_TransfersAndSaves()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var recipient = Guid.NewGuid();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();

        var result = await sut.Handle(
            new TransferTrainingCommand(training.Id.Value, recipient), CancellationToken.None);

        result.ShouldBeSuccess();
        training.TrainerId.Value.Should().Be(recipient);
        _trainingRepository.Verify(r => r.Update(training), Times.Once);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, a non existing training, returns not found.
    /// </summary>
    [Fact]
    public async Task Handle_ANonExistingTraining_ReturnsNotFound()
    {
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = CreateSut();

        var result = await sut.Handle(
            new TransferTrainingCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Handle, a ghost recipient, is refused before the service is consulted.
    /// </summary>
    [Fact]
    public async Task Handle_AGhostRecipient_IsRefusedBeforeTheServiceIsConsulted()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var formerOwner = training.TrainerId;
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        var result = await sut.Handle(
            new TransferTrainingCommand(training.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.UnknownRecipient);
        training.TrainerId.Should().Be(formerOwner);
        _trainingCounter.Verify(
            c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never, "existence is referential integrity, checked before the decision");
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, a domain refusal, propagates unchanged and saves nothing.
    /// </summary>
    [Fact]
    public async Task Handle_ADomainRefusal_PropagatesUnchangedAndSavesNothing()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _trainingCounter
            .Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Training.MaximumPerTrainer);
        var sut = CreateSut();

        var result = await sut.Handle(
            new TransferTrainingCommand(training.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.RecipientCatalogueFull);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, a lost uniqueness race at save, answers duplicate title.
    /// </summary>
    [Fact]
    public async Task Handle_ALostUniquenessRaceAtSave_AnswersDuplicateTitle()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _trainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _unitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintViolationException("duplicate", new InvalidOperationException()));
        var sut = CreateSut();

        var result = await sut.Handle(
            new TransferTrainingCommand(training.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.DuplicateTitle);
    }
}
