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

        trainer.Suspend().ShouldBeSuccess();

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
        trainer.Suspend().ShouldBeSuccess();
        trainer.ClearDomainEvents();

        var result = trainer.Suspend();

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
        trainer.Suspend().ShouldBeSuccess();
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

        trainer.Suspend().ShouldBeSuccess();
        trainer.Reinstate().ShouldBeSuccess();
        trainer.Suspend().ShouldBeSuccess();

        trainer.Status.Should().Be(TrainerStatus.Suspended);
    }
}
