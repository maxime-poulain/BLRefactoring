using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.IntegrationEventHandlers;
using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// The side effects, now running after the commit rather than before it.
/// </summary>
/// <remarks>
/// Same expectations as when this work lived in the domain event handlers — the emails and the
/// index calls did not change. What changed is when they happen, which these cannot observe;
/// that is the integration suite's job.
/// </remarks>
public class IntegrationEventHandlersTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    [Fact]
    public async Task TrainerRegistered_SendsWelcomeEmailToTheNewTrainer()
    {
        var sut = new SendWelcomeEmailWhenTrainerRegisteredHandler(_emailSender.Object);

        await sut.HandleAsync(new TrainerRegisteredIntegrationEvent(
            TrainerId.Generate(), "John", "Doe", "john.doe@example.com", DateTimeOffset.UtcNow));

        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "john.doe@example.com"
                    && message.Body.Contains("John")
                    && message.Body.Contains("Doe")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ContactEmailChanged_WarnsThePreviousAddress()
    {
        var sut = new NotifyPreviousAddressWhenContactEmailChangedHandler(_emailSender.Object);

        await sut.HandleAsync(new TrainerContactEmailChangedIntegrationEvent(
            TrainerId.Generate(), "old@example.com", "new@example.com", DateTimeOffset.UtcNow));

        // Sent to the old address on purpose: it is the one an attacker would not control.
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "old@example.com"
                    && message.Body.Contains("new@example.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TrainingPublished_IndexesTheTraining()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();
        var sut = new IndexTrainingWhenTrainingPublishedHandler(_searchIndexer.Object);

        await sut.HandleAsync(new TrainingPublishedIntegrationEvent(
            trainingId, trainerId, DateTimeOffset.UtcNow));

        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(trainingId.Value, trainerId.Value, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TrainingRevised_ReindexesTheTraining()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();
        var sut = new IndexTrainingWhenTrainingRevisedHandler(_searchIndexer.Object);

        await sut.HandleAsync(new TrainingRevisedIntegrationEvent(
            trainingId, trainerId, DateTimeOffset.UtcNow));

        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(trainingId.Value, trainerId.Value, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
