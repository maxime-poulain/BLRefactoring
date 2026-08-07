using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Publish;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Unpublish;
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
/// Behaviour covered for <c>PublishTrainingCommandHandler</c> and
/// <c>UnpublishTrainingCommandHandler</c>.
/// </summary>
/// <remarks>
/// Every rule these two commands answer to is the aggregate's and is proven in the domain suite.
/// What is left for a handler test is the orchestration: the training is found or the command says
/// not found, and a refusal leaves nothing committed.
/// </remarks>
public sealed class TrainingLifecycleCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<ITrainerStanding> _trainerStanding = new();
    private readonly Mock<ITrainingCounter> _trainingCounter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private PublishTrainingCommandHandler CreatePublishSut() => new(
        _trainingRepository.Object,
        _trainerStanding.Object,
        _trainingCounter.Object,
        _unitOfWork.Object);

    private UnpublishTrainingCommandHandler CreateUnpublishSut() =>
        new(_trainingRepository.Object, _unitOfWork.Object);

    private void Answers(Training? training) =>
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);

    /// <summary>
    /// Handle, an existing training, withdraws it and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_UnpublishAnExistingTraining_WithdrawsItAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        Answers(training);

        var result = await CreateUnpublishSut()
            .Handle(new UnpublishTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Unpublished);
        _trainingRepository.Verify(r => r.Update(training), Times.Once);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, unpublishing a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task Handle_UnpublishANonExistentTraining_AnswersNotFound()
    {
        Answers(null);

        var result = await CreateUnpublishSut()
            .Handle(new UnpublishTrainingCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(ErrorCodes.NotFound);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Handle, publishing a withdrawn training, offers it again and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_PublishAWithdrawnTraining_OffersItAgainAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Unpublish().ShouldBeSuccess();
        Answers(training);

        var result = await CreatePublishSut()
            .Handle(new PublishTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Published);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, publishing a training whose owner is suspended, commits nothing.
    /// </summary>
    [Fact]
    public async Task Handle_PublishForASuspendedOwner_CommitsNothing()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Unpublish().ShouldBeSuccess();
        Answers(training);
        _trainerStanding
            .Setup(s => s.IsSuspendedAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreatePublishSut()
            .Handle(new PublishTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.TrainerSuspended);
        training.Status.Should().Be(TrainingStatus.Unpublished);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
