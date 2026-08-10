using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for <c>DeleteTrainingWhenTrainerDeletedEventHandler</c>.
/// </summary>
public sealed class DeleteTrainingWhenTrainerDeletedEventHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();

    private DeleteTrainingWhenTrainerDeletedEventHandler CreateSut() =>
        new(_trainingRepository.Object);

    /// <summary>
    /// Handle, calls get by trainer id async.
    /// </summary>
    [Fact]
    public async Task Handle_CallsGetByTrainerIdAsync()
    {
        var trainer = new TrainerBuilder().Build();
        var domainEvent = new TrainerDeletedDomainEvent(trainer.Id);
        _trainingRepository
            .Setup(r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Training>());

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _trainingRepository.Verify(
            r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, calls delete async with retrieved trainings.
    /// </summary>
    [Fact]
    public async Task Handle_CallsDeleteAsyncWithRetrievedTrainings()
    {
        var trainer = new TrainerBuilder().Build();
        var domainEvent = new TrainerDeletedDomainEvent(trainer.Id);
        var trainings = new List<Training>();
        _trainingRepository
            .Setup(r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainings);

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _trainingRepository.Verify(r => r.Delete(trainings), Times.Once);
    }

    /// <summary>
    /// Handle, no trainings found, still calls delete async.
    /// </summary>
    [Fact]
    public async Task Handle_NoTrainingsFound_StillCallsDeleteAsync()
    {
        var trainer = new TrainerBuilder().Build();
        var domainEvent = new TrainerDeletedDomainEvent(trainer.Id);
        _trainingRepository
            .Setup(r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Training>());

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _trainingRepository.Verify(
            r => r.Delete(It.Is<IEnumerable<Training>>(c => !c.Any())),
            Times.Once);
    }
}
