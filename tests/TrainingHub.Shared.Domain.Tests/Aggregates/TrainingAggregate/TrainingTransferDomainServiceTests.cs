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
/// The one decision no aggregate can own: whether a training may change hands (ADR 0036).
/// </summary>
/// <remarks>
/// Every fact goes through <see cref="TrainingTransferDomainService.TransferAsync"/>, never through the
/// mutator — <c>Training.TransferTo</c> is internal and no <c>InternalsVisibleTo</c> exists, so
/// these tests double as the proof that the public path is sufficient.
/// </remarks>
public sealed class TrainingTransferDomainServiceTests
{
    private static readonly TrainerId Recipient = TrainerId.Generate();

    private static Mock<ITrainingCounter> CounterAnswering(int published)
    {
        var counter = new Mock<ITrainingCounter>();
        counter.Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(published);
        return counter;
    }

    private static Mock<IUniquenessTitleChecker> TitleCheckerAnswering(bool exists)
    {
        var checker = new Mock<IUniquenessTitleChecker>();
        checker.Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(), It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
        return checker;
    }

    private static Mock<ITrainerStanding> StandingAnswering(bool suspended)
    {
        var standing = new Mock<ITrainerStanding>();
        standing.Setup(s => s.IsSuspendedAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);
        return standing;
    }

    /// <summary>
    /// Transfer async, a recipient with room and a free title, reassigns the owner.
    /// </summary>
    [Fact]
    public async Task TransferAsync_RecipientHasRoomAndTheTitleIsFree_ReassignsTheOwner()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        var result = await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(0).Object, TitleCheckerAnswering(false).Object, StandingAnswering(false).Object);

        result.ShouldBeSuccess();
        training.TrainerId.Should().Be(Recipient);
    }

    /// <summary>
    /// Transfer async, success, raises the transferred event carrying both owners.
    /// </summary>
    [Fact]
    public async Task TransferAsync_Success_RaisesTheTransferredEvent_CarryingBothOwners()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var formerOwner = training.TrainerId;

        (await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(0).Object, TitleCheckerAnswering(false).Object, StandingAnswering(false).Object))
            .ShouldBeSuccess();

        var fact = training.DomainEvents.Should()
            .ContainSingle(e => e is TrainingTransferredDomainEvent).Which
            .Should().BeOfType<TrainingTransferredDomainEvent>().Subject;
        fact.TrainingId.Should().Be(training.Id);
        fact.FormerTrainerId.Should().Be(formerOwner, "by delivery time, nothing else remembers the former owner");
        fact.NewTrainerId.Should().Be(Recipient);
        training.DomainEvents.Should().NotContain(e => e is TrainingEditedDomainEvent,
            "a transfer changes hands, not content");
    }

    /// <summary>
    /// Transfer async, a recipient at the limit, answers recipient catalogue full.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ARecipientAtTheLimit_AnswersRecipientCatalogueFull()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var formerOwner = training.TrainerId;

        var result = await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(Training.MaximumPerTrainer).Object,
            TitleCheckerAnswering(false).Object, StandingAnswering(false).Object);

        var error = result.ShouldBeFailure().Should().ContainSingle().Which;
        error.ErrorCode.Should().Be(TrainingErrorCodes.RecipientCatalogueFull);
        training.TrainerId.Should().Be(formerOwner, "a refusal mutates nothing");
        training.DomainEvents.Should().NotContain(e => e is TrainingTransferredDomainEvent);
    }

    /// <summary>
    /// Transfer async, a recipient one under the limit, still receives.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ARecipientOneUnderTheLimit_StillReceives()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        var result = await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(Training.MaximumPerTrainer - 1).Object,
            TitleCheckerAnswering(false).Object, StandingAnswering(false).Object);

        // Nine published trainings leave room for a tenth: the boundary is >= 10, not > 9.
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Transfer async, a full recipient, refuses before asking the title.
    /// </summary>
    [Fact]
    public async Task TransferAsync_AFullRecipient_RefusesBeforeAskingTheTitle()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var titleChecker = TitleCheckerAnswering(true);

        var result = await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(Training.MaximumPerTrainer).Object, titleChecker.Object,
            StandingAnswering(false).Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.RecipientCatalogueFull);
        titleChecker.Verify(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(), It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never, "no title makes an eleventh training acceptable — capacity refuses whole");
    }

    /// <summary>
    /// Transfer async, a recipient with the same title, answers duplicate title.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ARecipientWithTheSameTitle_AnswersDuplicateTitle()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var formerOwner = training.TrainerId;

        var result = await TrainingTransferDomainService.TransferAsync(
            training, Recipient, CounterAnswering(0).Object, TitleCheckerAnswering(true).Object, StandingAnswering(false).Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.DuplicateTitle);
        training.TrainerId.Should().Be(formerOwner);
        training.DomainEvents.Should().NotContain(e => e is TrainingTransferredDomainEvent);
    }

    /// <summary>
    /// Transfer async, a transfer to the current owner, answers transfer to self and asks no port.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ATransferToTheCurrentOwner_AnswersTransferToSelf()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var counter = CounterAnswering(0);
        var titleChecker = TitleCheckerAnswering(false);

        var result = await TrainingTransferDomainService.TransferAsync(
            training, training.TrainerId, counter.Object, titleChecker.Object, StandingAnswering(false).Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.TransferToSelf);
        counter.Verify(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never, "the recipient-side questions are vacuous when recipient and owner are one");
        titleChecker.Verify(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(), It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Transfer async, null arguments, throw.
    /// </summary>
    [Fact]
    public async Task TransferAsync_NullArguments_ThrowArgumentNullException()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var counter = CounterAnswering(0).Object;
        var titleChecker = TitleCheckerAnswering(false).Object;
        var standing = StandingAnswering(false).Object;

        var acts = new Func<Task>[]
        {
            () => TrainingTransferDomainService.TransferAsync(null!, Recipient, counter, titleChecker, standing),
            () => TrainingTransferDomainService.TransferAsync(training, null!, counter, titleChecker, standing),
            () => TrainingTransferDomainService.TransferAsync(training, Recipient, null!, titleChecker, standing),
            () => TrainingTransferDomainService.TransferAsync(training, Recipient, counter, null!, standing),
            () => TrainingTransferDomainService.TransferAsync(training, Recipient, counter, titleChecker, null!),
        };

        foreach (var act in acts)
        {
            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
