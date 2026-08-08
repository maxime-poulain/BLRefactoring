using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate;

/// <summary>
/// The sanction ADR 0050 gives a trainer: one field, both directions, and no trainings touched.
/// </summary>
/// <remarks>
/// No endpoint reaches these methods, deliberately and for the reason <c>MarkForDeletion</c> gives:
/// suspending is an administrative decision, and none exists until a role is entitled to it. What
/// is proven here is the rule itself, which holds whoever ends up triggering it.
/// </remarks>
public sealed class TrainerStandingTests
{
    /// <summary>
    /// A reason good enough for every fact here, since none of them is about the reason itself —
    /// those live in <c>SuspensionReasonTests</c>.
    /// </summary>
    private static SuspensionReason AReason =>
        SuspensionReason.Create("Repeated breaches of the content policy.").ShouldBeSuccess();

    /// <summary>
    /// Create, a new trainer, is born active.
    /// </summary>
    [Fact]
    public void Create_ANewTrainer_IsBornActive()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.Status.Should().Be(TrainerStatus.Active);
    }

    /// <summary>
    /// Suspend, an active trainer, sanctions them and announces the fact.
    /// </summary>
    [Fact]
    public void Suspend_AnActiveTrainer_SanctionsThemAndAnnouncesTheFact()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        trainer.Suspend(AReason).ShouldBeSuccess();

        trainer.Status.Should().Be(TrainerStatus.Suspended);
        trainer.DomainEvents.Should()
            .ContainSingle(e => e is TrainerSuspendedDomainEvent).Which
            .Should().BeOfType<TrainerSuspendedDomainEvent>().Which
            .TrainerId.Should().Be(trainer.Id);
    }

    /// <summary>
    /// Suspend, an already suspended trainer, is refused and announces nothing.
    /// </summary>
    [Fact]
    public void Suspend_AnAlreadySuspendedTrainer_IsRefusedAndAnnouncesNothing()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(AReason).ShouldBeSuccess();
        trainer.ClearDomainEvents();

        var result = trainer.Suspend(AReason);

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainerErrorCodes.AlreadySuspended);
        trainer.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Reinstate, a suspended trainer, lifts the sanction and announces the fact.
    /// </summary>
    [Fact]
    public void Reinstate_ASuspendedTrainer_LiftsTheSanctionAndAnnouncesTheFact()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(AReason).ShouldBeSuccess();
        trainer.ClearDomainEvents();

        trainer.Reinstate().ShouldBeSuccess();

        trainer.Status.Should().Be(TrainerStatus.Active);
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerReinstatedDomainEvent);
    }

    /// <summary>
    /// Reinstate, a trainer who is not suspended, is refused.
    /// </summary>
    [Fact]
    public void Reinstate_ATrainerWhoIsNotSuspended_IsRefused()
    {
        var trainer = new TrainerBuilder().Build();

        var result = trainer.Reinstate();

        result.ShouldBeFailure().Should().ContainSingle()
            .Which.ErrorCode.Should().Be(TrainerErrorCodes.NotSuspended);
    }

    /// <summary>
    /// A trainer, travels both ways — which is what makes the sanction liftable.
    /// </summary>
    /// <remarks>
    /// The scenario that decided the shape of the whole record: a sanction indistinguishable from
    /// an erasure cannot be lifted, and one that rewrote the catalogue on the way in could not put
    /// it back the way it was.
    /// </remarks>
    [Fact]
    public void ATrainer_TravelsBothWays()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.Suspend(AReason).ShouldBeSuccess();
        trainer.Reinstate().ShouldBeSuccess();
        trainer.Suspend(AReason).ShouldBeSuccess();

        trainer.Status.Should().Be(TrainerStatus.Suspended);
    }

    /// <summary>
    /// The reason, is present exactly while the sanction is.
    /// </summary>
    /// <remarks>
    /// Walked as one sequence rather than asserted twice, because the claim is about the pair
    /// moving together: a mute sanction and an orphan reason are the two ways ADR 0052's invariant
    /// can rot, and each of them is a step of this walk.
    /// </remarks>
    [Fact]
    public void TheReason_IsPresentExactlyWhileTheSanctionIs()
    {
        var trainer = new TrainerBuilder().Build();

        trainer.SuspensionReason.Should().BeNull("a trainer in good standing carries no reason");

        trainer.Suspend(AReason).ShouldBeSuccess();
        trainer.SuspensionReason.Should().Be(AReason);

        trainer.Reinstate().ShouldBeSuccess();
        trainer.SuspensionReason.Should().BeNull("lifting the sanction takes its reason with it");
    }

    /// <summary>
    /// Suspend, announces the reason with the fact.
    /// </summary>
    /// <remarks>
    /// The audit trail is the only record of who suspended whom and why (ADR 0027 stamps the actor,
    /// ADR 0052 adds the motive), and it can only carry what the fact carries.
    /// </remarks>
    [Fact]
    public void Suspend_AnnouncesTheReasonWithTheFact()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        trainer.Suspend(AReason).ShouldBeSuccess();

        trainer.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TrainerSuspendedDomainEvent>()
            .Which.Reason.Should().Be(AReason);
    }
}
