using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainingAggregate;

/// <summary>
/// The third state ADR 0052 gives a training, and the one thing it exists to make impossible.
/// </summary>
/// <remarks>
/// Setting <see cref="TrainingStatus.Unpublished"/> would have been cheaper and worth nothing:
/// <c>POST /Training/{id}/publish</c> belongs to the owner, so an administration that merely
/// withdrew a training would be undone in one request. Every fact below is about that difference —
/// the owner can leave the two states they chose, and neither door out of the third one opens for
/// them.
/// </remarks>
public sealed class TrainingWithholdingTests
{
    private static WithholdingReason AReason =>
        WithholdingReason.Create("Contains material the author does not hold the rights to.")
            .ShouldBeSuccess();

    private static Mock<ITrainerStanding> Standing(bool suspended = false)
    {
        var standing = new Mock<ITrainerStanding>();
        standing.Setup(s => s.IsSuspendedAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);
        return standing;
    }

    private static Mock<ITrainingCounter> Counter(int counted = 0)
    {
        var counter = new Mock<ITrainingCounter>();
        counter.Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(counted);
        return counter;
    }

    private static async Task<Training> AWithheldTrainingAsync()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Withhold(AReason).ShouldBeSuccess();
        training.ClearDomainEvents();
        return training;
    }

    // -- Withhold --

    /// <summary>
    /// Withhold, a published training, moves it and announces the reason.
    /// </summary>
    [Fact]
    public async Task Withhold_APublishedTraining_MovesItAndAnnouncesTheReason()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();

        training.Withhold(AReason).ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Withheld);
        training.WithholdingReason.Should().Be(AReason);
        training.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TrainingWithheldDomainEvent>()
            .Which.Reason.Should().Be(AReason);
    }

    /// <summary>
    /// Withhold, an already withdrawn training, is still permitted.
    /// </summary>
    /// <remarks>
    /// The starting state that a "you can only withhold what is on offer" reading would have
    /// refused, and it is the one that matters: prohibited content is prohibited whether or not it
    /// is currently being offered, and withholding an already-withdrawn training is precisely what
    /// stops its owner putting it back.
    /// </remarks>
    [Fact]
    public async Task Withhold_AnAlreadyWithdrawnTraining_IsStillPermitted()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Unpublish().ShouldBeSuccess();

        training.Withhold(AReason).ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Withheld);
    }

    /// <summary>
    /// Withhold, a training already withheld, is refused.
    /// </summary>
    [Fact]
    public async Task Withhold_ATrainingAlreadyWithheld_IsRefused()
    {
        var training = await AWithheldTrainingAsync();

        training.Withhold(AReason).ShouldContainError(TrainingErrorCodes.AlreadyWithheld);
        training.DomainEvents.Should().BeEmpty("a change that changes nothing raises no fact");
    }

    // -- Release --

    /// <summary>
    /// Release, a withheld training, lands it unpublished and clears the reason.
    /// </summary>
    /// <remarks>
    /// Never straight back on offer, and no memory of where it came from. The administration lifts
    /// an interdiction; putting a training back in the window is its owner's decision.
    /// </remarks>
    [Fact]
    public async Task Release_AWithheldTraining_LandsItUnpublishedAndClearsTheReason()
    {
        var training = await AWithheldTrainingAsync();

        training.Release().ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Unpublished);
        training.WithholdingReason.Should().BeNull();
        training.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TrainingReleasedDomainEvent>();
    }

    /// <summary>
    /// Release, a training that was published, still lands it unpublished.
    /// </summary>
    /// <remarks>
    /// The asymmetry stated as a fact rather than left to the prose: a training withheld while on
    /// offer does not go back on offer when the interdiction is lifted. Restoring the prior state
    /// would need a field written on every withholding and read once, which is what ADR 0052
    /// refused.
    /// </remarks>
    [Fact]
    public async Task Release_ATrainingThatWasPublished_StillLandsItUnpublished()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Status.Should().Be(TrainingStatus.Published, "a training is born published");
        training.Withhold(AReason).ShouldBeSuccess();

        training.Release().ShouldBeSuccess();

        training.Status.Should().Be(TrainingStatus.Unpublished);
    }

    /// <summary>
    /// Release, a training that is not withheld, is refused.
    /// </summary>
    [Fact]
    public async Task Release_ATrainingThatIsNotWithheld_IsRefused()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();

        training.Release().ShouldContainError(TrainingErrorCodes.NotWithheld);
        training.DomainEvents.Should().BeEmpty();
    }

    // -- The owner is locked out, by both doors --

    /// <summary>
    /// Publish async, a withheld training, is refused by name.
    /// </summary>
    /// <remarks>
    /// The reason the third state exists at all. It is refused before the standing and the capacity
    /// are even asked, because none of those questions could make this one acceptable.
    /// </remarks>
    [Fact]
    public async Task PublishAsync_AWithheldTraining_IsRefusedByName()
    {
        var training = await AWithheldTrainingAsync();

        var result = await training.PublishAsync(Standing().Object, Counter().Object);

        result.ShouldContainError(TrainingErrorCodes.Withheld);
        training.Status.Should().Be(TrainingStatus.Withheld);
        training.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Unpublish, a withheld training, is refused too.
    /// </summary>
    /// <remarks>
    /// The second door, and the one a reader would not think to check. Refusing only in
    /// <c>PublishAsync</c> would leave the interdiction liftable in two moves — unpublish, then
    /// publish — each of them through a check that passed, which is exactly the shape of the quota
    /// bypass ADR 0050 found.
    /// </remarks>
    [Fact]
    public async Task Unpublish_AWithheldTraining_IsRefusedToo()
    {
        var training = await AWithheldTrainingAsync();

        training.Unpublish().ShouldContainError(TrainingErrorCodes.Withheld);

        training.Status.Should().Be(TrainingStatus.Withheld);
        training.WithholdingReason.Should().Be(AReason, "the reason survives an attempt to shed it");
    }

    // -- The invariant, in both directions --

    /// <summary>
    /// The reason, is present exactly while the state is.
    /// </summary>
    /// <remarks>
    /// Walked as one sequence rather than asserted twice, because the claim is about the pair
    /// moving together: a mute withheld state and an orphan reason are the two ways this can rot,
    /// and each of them is a step of this walk.
    /// </remarks>
    [Fact]
    public async Task TheReason_IsPresentExactlyWhileTheStateIs()
    {
        var training = await new TrainingBuilder().BuildValidAsync();

        training.WithholdingReason.Should().BeNull("a training nobody withheld carries no reason");

        training.Withhold(AReason).ShouldBeSuccess();
        training.WithholdingReason.Should().NotBeNull();

        training.Release().ShouldBeSuccess();
        training.WithholdingReason.Should().BeNull("releasing takes the reason with the state");
    }

    // -- The quota --

    /// <summary>
    /// A withheld training, keeps its place in the quota.
    /// </summary>
    /// <remarks>
    /// The half of ADR 0052 that had to land in the same commit as the state itself. ADR 0050 made
    /// the ten count what the public can see, arguing that a trainer who withdraws ten should not
    /// lose their catalog for ever — but that argument is about a <em>voluntary</em> withdrawal.
    /// Freeing a slot by being moderated is a perverse incentive, so the question became "did its
    /// owner take it down" rather than "is it published".
    /// <para>
    /// Asserted through the specification in memory and as an expression compiled and run, which is
    /// the dual evaluation ADR 0028 exists for: the aggregate's rule and the database's filter are
    /// one expression and cannot drift.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AWithheldTraining_KeepsItsPlaceInTheQuota()
    {
        var withheld = await AWithheldTrainingAsync();
        var published = await new TrainingBuilder().BuildValidAsync();
        var withdrawn = await new TrainingBuilder().BuildValidAsync();
        withdrawn.Unpublish().ShouldBeSuccess();

        var specification = new TrainingCountsTowardTheQuotaSpecification();

        specification.IsSatisfiedBy(withheld).Should().BeTrue("being moderated does not free a slot");
        specification.IsSatisfiedBy(published).Should().BeTrue();
        specification.IsSatisfiedBy(withdrawn).Should().BeFalse("only the owner's own withdrawal does");

        // The same expression, compiled — what a provider would translate. One rule, two readings.
        var criteria = specification.Criteria.Compile();

        criteria(withheld).Should().BeTrue();
        criteria(published).Should().BeTrue();
        criteria(withdrawn).Should().BeFalse();
    }
}
