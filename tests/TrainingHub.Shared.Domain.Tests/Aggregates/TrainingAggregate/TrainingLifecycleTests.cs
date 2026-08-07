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
/// The transitions ADR 0050 gives a training, and the three invariants it changes: the quota counts
/// published trainings, a withdrawn training keeps its title, and a suspended trainer may not grow
/// what the public sees.
/// </summary>
/// <remarks>
/// What separates a lifecycle from a soft delete is that both directions exist and each announces
/// itself. Both halves are asserted here — a transition that changed the state without raising a
/// fact would pass a "the status is now Unpublished" test and leave the search index serving a
/// training nobody is being offered.
/// </remarks>
public sealed class TrainingLifecycleTests
{
    private static Mock<ITrainerStanding> Standing(bool suspended)
    {
        var standing = new Mock<ITrainerStanding>();
        standing.Setup(s => s.IsSuspendedAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);
        return standing;
    }

    private static Mock<ITrainingCounter> Counter(int published)
    {
        var counter = new Mock<ITrainingCounter>();
        counter.Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(published);
        return counter;
    }

    private static async Task<Training> AWithdrawnTrainingAsync()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Unpublish().ShouldBeSuccess();
        training.ClearDomainEvents();
        return training;
    }

    /// <summary>
    /// Create async, a new training, is born published.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ANewTraining_IsBornPublished()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        training.Status.Should().Be(TrainingStatus.Published);
    }

    /// <summary>
    /// Unpublish, a published training, withdraws it and announces the fact.
    /// </summary>
    [Fact]
    public async Task Unpublish_APublishedTraining_WithdrawsItAndAnnouncesTheFact()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        training.Unpublish().ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Unpublished);
        var fact = training.DomainEvents.Should()
            .ContainSingle(e => e is TrainingUnpublishedDomainEvent).Which
            .Should().BeOfType<TrainingUnpublishedDomainEvent>().Which;
        fact.TrainingId.Should().Be(training.Id);
        fact.TrainerId.Should().Be(training.TrainerId);
    }

    /// <summary>
    /// Unpublish, a training keeps its title, so the owner can still find it.
    /// </summary>
    [Fact]
    public async Task Unpublish_ATraining_KeepsItsTitle()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var title = training.Title;

        training.Unpublish().ShouldBeSuccess();

        // The title stays taken, deliberately: it is taken by something its owner can see in their
        // own listing and can republish, rename or delete — so a duplicate-title refusal names
        // something actionable rather than something invisible (ADR 0050).
        training.Title.Should().Be(title);
    }

    /// <summary>
    /// Unpublish, an already withdrawn training, is refused and announces nothing.
    /// </summary>
    [Fact]
    public async Task Unpublish_AnAlreadyWithdrawnTraining_IsRefusedAndAnnouncesNothing()
    {
        var training = await AWithdrawnTrainingAsync();

        var result = training.Unpublish();

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.AlreadyUnpublished);
        training.DomainEvents.Should().BeEmpty("a change that changes nothing announces nothing");
    }

    /// <summary>
    /// Publish async, a withdrawn training, offers it again and announces the fact.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AWithdrawnTraining_OffersItAgainAndAnnouncesTheFact()
    {
        var training = await AWithdrawnTrainingAsync();

        (await training.PublishAsync(Standing(false).Object, Counter(0).Object)).ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Published);
        training.DomainEvents.Should().ContainSingle(e => e is TrainingPublishedDomainEvent);
    }

    /// <summary>
    /// Publish async, an already published training, is refused before any port is asked.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AnAlreadyPublishedTraining_IsRefusedBeforeAnyPortIsAsked()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();
        var standing = Standing(false);
        var counter = Counter(0);

        var result = await training.PublishAsync(standing.Object, counter.Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.AlreadyPublished);
        training.DomainEvents.Should().BeEmpty();
        standing.Verify(s => s.IsSuspendedAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never, "the rules below are vacuous when the training is already where it is going");
        counter.Verify(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Publish async, a suspended owner, is refused.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ASuspendedOwner_IsRefused()
    {
        var training = await AWithdrawnTrainingAsync();

        var result = await training.PublishAsync(Standing(true).Object, Counter(0).Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.TrainerSuspended);
        training.Status.Should().Be(TrainingStatus.Unpublished);
        training.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Publish async, an owner at the maximum, is refused — the hole a quota on published trainings
    /// would otherwise leave open.
    /// </summary>
    /// <remarks>
    /// Without this rule the limit is bypassable in three moves, each of which passes a check:
    /// unpublish one of ten, create a replacement, republish the first. Eleven trainings on offer.
    /// </remarks>
    [Fact]
    public async Task PublishAsync_AnOwnerAtTheMaximum_IsRefused()
    {
        var training = await AWithdrawnTrainingAsync();

        var result = await training.PublishAsync(
            Standing(false).Object, Counter(Training.MaximumPerTrainer).Object);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.CatalogueFull);
        training.Status.Should().Be(TrainingStatus.Unpublished);
    }

    /// <summary>
    /// Unpublish, asks nothing about its owner, so a suspended trainer may still withdraw.
    /// </summary>
    /// <remarks>
    /// The half of the sanction that is easy to get wrong. Withdrawing shrinks what the public sees,
    /// so no standing rule may stand in its way — which is what leaves a suspended trainer able to
    /// repair what earned them the sanction.
    /// <para>
    /// Asserted on the signature rather than by suspending a trainer and calling the method, and
    /// the difference matters: a behavioural test would pass just as happily against a version that
    /// consulted the port and happened to allow this case, and would go on passing when somebody
    /// later made that consultation refuse. A method with no port cannot be made to ask.
    /// </para>
    /// </remarks>
    [Fact]
    public void Unpublish_AsksNothingAboutItsOwner()
    {
        var unpublish = typeof(Training).GetMethod(nameof(Training.Unpublish));

        unpublish!.GetParameters().Should().BeEmpty(
            "withdrawing shrinks the public catalogue, so no fact about the owner can refuse it");
    }

    /// <summary>
    /// Create async, a suspended trainer, is refused before the catalogue is measured.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ASuspendedTrainer_IsRefused()
    {
        var result = await new TrainingBuilder().WithSuspendedTrainer().BuildAsync();

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainingErrorCodes.TrainerSuspended);
    }

    /// <summary>
    /// Mark for deletion, announces the fact deletion never used to raise.
    /// </summary>
    /// <remarks>
    /// The absence of this event is why a deleted training stayed in the search index for ever:
    /// nothing announced the deletion, so no consumer could remove it.
    /// </remarks>
    [Fact]
    public async Task MarkForDeletion_AnnouncesTheDeletion()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();

        training.MarkForDeletion();

        var fact = training.DomainEvents.Should()
            .ContainSingle(e => e is TrainingDeletedDomainEvent).Which
            .Should().BeOfType<TrainingDeletedDomainEvent>().Which;
        fact.TrainingId.Should().Be(training.Id);
        fact.TrainerId.Should().Be(training.TrainerId);
    }

    /// <summary>
    /// Publish async, then unpublish, returns to where it started — which is what makes this a
    /// lifecycle rather than a tombstone.
    /// </summary>
    [Fact]
    public async Task ATraining_TravelsBothWays()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        training.Unpublish().ShouldBeSuccess();
        (await training.PublishAsync(Standing(false).Object, Counter(0).Object)).ShouldBeSuccess();
        training.Unpublish().ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Unpublished);
    }
}
