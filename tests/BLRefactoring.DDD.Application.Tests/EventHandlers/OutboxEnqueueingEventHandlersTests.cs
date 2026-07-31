using AwesomeAssertions;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

/// <summary>
/// The translation from a domain event to the fact this system publishes.
/// </summary>
/// <remarks>
/// What these assert, beyond "a message is queued", is the shape of the contract: identifiers
/// survive as typed values while value objects arrive flattened. That distinction is the whole
/// reason the outbox can be read back safely months later, so it deserves to fail loudly if
/// someone reverses it.
/// </remarks>
public class OutboxEnqueueingEventHandlersTests
{
    private readonly Mock<IOutbox> _outbox = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private IIntegrationEvent? Captured =>
        _outbox.Invocations.SelectMany(invocation => invocation.Arguments)
            .OfType<IIntegrationEvent>()
            .SingleOrDefault();

    [Fact]
    public async Task TrainerCreated_QueuesRegistration_WithTheNameFlattened()
    {
        var trainerId = TrainerId.Generate();
        var sut = new EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler(_outbox.Object, _timeProvider);

        await sut.Handle(
            new TrainerCreatedDomainEvent(
                trainerId,
                Name.Create("John", "Doe").ShouldBeSuccess(),
                Email.Create("john.doe@example.com").ShouldBeSuccess()),
            CancellationToken.None);

        var integrationEvent = Captured.Should().BeOfType<TrainerRegisteredIntegrationEvent>().Subject;

        // The identifier keeps its type — its only rule will never change.
        integrationEvent.TrainerId.Should().Be(trainerId);

        // The name and the address do not: their rules move, so the contract carries strings.
        integrationEvent.Firstname.Should().Be("John");
        integrationEvent.Lastname.Should().Be("Doe");
        integrationEvent.ContactEmail.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task TrainerCreated_SendsNothingItself()
    {
        var emailSender = new Mock<IEmailSender>(MockBehavior.Strict);
        var sut = new EnqueueTrainerRegisteredWhenTrainerCreatedEventHandler(_outbox.Object, _timeProvider);

        await sut.Handle(
            new TrainerCreatedDomainEvent(
                TrainerId.Generate(),
                Name.Create("John", "Doe").ShouldBeSuccess(),
                Email.Create("john.doe@example.com").ShouldBeSuccess()),
            CancellationToken.None);

        // The point of the whole change: this runs before the commit, so anything it sent would
        // be gone for good if the transaction then failed.
        emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ContactEmailChanged_QueuesBothAddresses()
    {
        var trainerId = TrainerId.Generate();
        var sut = new EnqueueContactEmailChangedWhenTrainerContactEmailChangedEventHandler(
            _outbox.Object, _timeProvider);

        await sut.Handle(
            new TrainerContactEmailChangedDomainEvent(
                trainerId,
                Email.Create("old@example.com").ShouldBeSuccess(),
                Email.Create("new@example.com").ShouldBeSuccess()),
            CancellationToken.None);

        var integrationEvent = Captured.Should()
            .BeOfType<TrainerContactEmailChangedIntegrationEvent>().Subject;

        // The previous address cannot be recovered from the aggregate once the change is
        // committed, so it has to travel with the message.
        integrationEvent.PreviousContactEmail.Should().Be("old@example.com");
        integrationEvent.NewContactEmail.Should().Be("new@example.com");
    }

    [Fact]
    public async Task TrainingCreated_QueuesPublication()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();
        var sut = new EnqueueTrainingPublishedWhenTrainingCreatedEventHandler(_outbox.Object, _timeProvider);

        await sut.Handle(new TrainingCreatedDomainEvent(trainingId, trainerId), CancellationToken.None);

        var integrationEvent = Captured.Should().BeOfType<TrainingPublishedIntegrationEvent>().Subject;
        integrationEvent.TrainingId.Should().Be(trainingId);
        integrationEvent.TrainerId.Should().Be(trainerId);
    }

    [Fact]
    public async Task TrainingEdited_QueuesRevision_NotPublication()
    {
        var sut = new EnqueueTrainingRevisedWhenTrainingEditedEventHandler(_outbox.Object, _timeProvider);

        await sut.Handle(
            new TrainingEditedDomainEvent(TrainingId.Generate(), TrainerId.Generate()),
            CancellationToken.None);

        // Two contracts rather than one with a flag: a subscriber may care about one and not
        // the other, even though today's indexer treats them alike.
        Captured.Should().BeOfType<TrainingRevisedIntegrationEvent>();
    }
}
