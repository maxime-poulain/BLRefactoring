using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Reinstate;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Suspend;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Release;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Withhold;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for the four administrative command handlers.
/// </summary>
/// <remarks>
/// Every rule these four commands answer to is the aggregate's or the reason's, and both are proven
/// in the domain suite. What is left for a handler test is the orchestration, and one thing more
/// that only a handler can get wrong: the reason is judged <em>before</em> the aggregate is asked
/// anything, so a motive the domain refuses leaves the trainer's status untouched and commits
/// nothing.
/// </remarks>
public sealed class ModerationCommandHandlerTests
{
    private const string Motive = "Repeated breaches of the content policy.";

    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private SuspendTrainerCommandHandler CreateSuspendSut() =>
        new(_trainerRepository.Object, _unitOfWork.Object);

    private ReinstateTrainerCommandHandler CreateReinstateSut() =>
        new(_trainerRepository.Object, _unitOfWork.Object);

    private WithholdTrainingCommandHandler CreateWithholdSut() =>
        new(_trainingRepository.Object, _unitOfWork.Object);

    private ReleaseTrainingCommandHandler CreateReleaseSut() =>
        new(_trainingRepository.Object, _unitOfWork.Object);

    private void Answers(Trainer? trainer) =>
        _trainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);

    private void Answers(Training? training) =>
        _trainingRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);

    /// <summary>
    /// Handle, suspending an active trainer, records the reason and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_SuspendAnActiveTrainer_RecordsTheReasonAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        Answers(trainer);

        var result = await CreateSuspendSut()
            .Handle(new SuspendTrainerCommand(trainer.Id.Value, Motive), CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Status.Should().Be(TrainerStatus.Suspended);
        trainer.SuspensionReason!.Value.Should().Be(Motive);
        _trainerRepository.Verify(repository => repository.Update(trainer), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, suspending a trainer that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task Handle_SuspendANonExistentTrainer_AnswersNotFound()
    {
        Answers((Trainer?)null);

        var result = await CreateSuspendSut()
            .Handle(new SuspendTrainerCommand(Guid.NewGuid(), Motive), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, suspending with a reason the domain refuses, leaves the trainer active.
    /// </summary>
    /// <remarks>
    /// The order is the point. Were the sanction applied first and the reason judged afterwards,
    /// this trainer would be suspended with nothing on record — the mute state ADR 0052 forbids.
    /// </remarks>
    [Fact]
    public async Task Handle_SuspendWithARefusedReason_LeavesTheTrainerActive()
    {
        var trainer = new TrainerBuilder().Build();
        Answers(trainer);

        var result = await CreateSuspendSut()
            .Handle(new SuspendTrainerCommand(trainer.Id.Value, "   "), CancellationToken.None);

        result.ShouldContainError(TrainerErrorCodes.SuspensionReasonEmpty);
        trainer.Status.Should().Be(TrainerStatus.Active);
        trainer.SuspensionReason.Should().BeNull();
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, suspending a trainer that is already suspended, commits nothing.
    /// </summary>
    [Fact]
    public async Task Handle_SuspendAnAlreadySuspendedTrainer_CommitsNothing()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(SuspensionReason.Create("A first sanction.").ShouldBeSuccess()).ShouldBeSuccess();
        Answers(trainer);

        var result = await CreateSuspendSut()
            .Handle(new SuspendTrainerCommand(trainer.Id.Value, Motive), CancellationToken.None);

        result.ShouldContainError(TrainerErrorCodes.AlreadySuspended);
        trainer.SuspensionReason!.Value.Should().Be("A first sanction.");
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, reinstating a suspended trainer, clears the reason and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_ReinstateASuspendedTrainer_ClearsTheReasonAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(SuspensionReason.Create(Motive).ShouldBeSuccess()).ShouldBeSuccess();
        Answers(trainer);

        var result = await CreateReinstateSut()
            .Handle(new ReinstateTrainerCommand(trainer.Id.Value), CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Status.Should().Be(TrainerStatus.Active);
        trainer.SuspensionReason.Should().BeNull();
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, reinstating a trainer that is not under sanction, commits nothing.
    /// </summary>
    [Fact]
    public async Task Handle_ReinstateAnActiveTrainer_CommitsNothing()
    {
        var trainer = new TrainerBuilder().Build();
        Answers(trainer);

        var result = await CreateReinstateSut()
            .Handle(new ReinstateTrainerCommand(trainer.Id.Value), CancellationToken.None);

        result.ShouldContainError(TrainerErrorCodes.NotSuspended);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, reinstating a trainer that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task Handle_ReinstateANonExistentTrainer_AnswersNotFound()
    {
        Answers((Trainer?)null);

        var result = await CreateReinstateSut()
            .Handle(new ReinstateTrainerCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, withholding a published training, records the reason and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_WithholdAPublishedTraining_RecordsTheReasonAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        Answers(training);

        var result = await CreateWithholdSut()
            .Handle(new WithholdTrainingCommand(training.Id.Value, Motive), CancellationToken.None);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Withheld);
        training.WithholdingReason!.Value.Should().Be(Motive);
        _trainingRepository.Verify(repository => repository.Update(training), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, withholding with a reason the domain refuses, leaves the training on offer.
    /// </summary>
    [Fact]
    public async Task Handle_WithholdWithARefusedReason_LeavesTheTrainingOnOffer()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        Answers(training);

        var result = await CreateWithholdSut()
            .Handle(new WithholdTrainingCommand(training.Id.Value, new string('x', 501)), CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.WithholdingReasonTooLong);
        training.Status.Should().Be(TrainingStatus.Published);
        training.WithholdingReason.Should().BeNull();
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, withholding a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task Handle_WithholdANonExistentTraining_AnswersNotFound()
    {
        Answers((Training?)null);

        var result = await CreateWithholdSut()
            .Handle(new WithholdTrainingCommand(Guid.NewGuid(), Motive), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, releasing a withheld training, lands it on unpublished and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_ReleaseAWithheldTraining_LandsItOnUnpublishedAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Withhold(WithholdingReason.Create(Motive).ShouldBeSuccess()).ShouldBeSuccess();
        Answers(training);

        var result = await CreateReleaseSut()
            .Handle(new ReleaseTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Unpublished);
        training.WithholdingReason.Should().BeNull();
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, releasing a training that was not withheld, commits nothing.
    /// </summary>
    [Fact]
    public async Task Handle_ReleaseATrainingThatIsNotWithheld_CommitsNothing()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        Answers(training);

        var result = await CreateReleaseSut()
            .Handle(new ReleaseTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldContainError(TrainingErrorCodes.NotWithheld);
        training.Status.Should().Be(TrainingStatus.Published);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, releasing a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task Handle_ReleaseANonExistentTraining_AnswersNotFound()
    {
        Answers((Training?)null);

        var result = await CreateReleaseSut()
            .Handle(new ReleaseTrainingCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
