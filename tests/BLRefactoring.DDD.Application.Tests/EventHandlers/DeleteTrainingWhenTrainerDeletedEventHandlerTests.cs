using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

public class DeleteTrainingWhenTrainerDeletedEventHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();

    private DeleteTrainingWhenTrainerDeletedEventHandler CreateSut() =>
        new(_trainingRepository.Object);

    [Fact]
    public async Task Handle_CallsGetByTrainerIdAsync()
    {
        var trainer = new TrainerBuilder().BuildValid();
        var domainEvent = new TrainerDeletedDomainEvent(trainer);
        _trainingRepository
            .Setup(r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Training>());

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _trainingRepository.Verify(
            r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CallsDeleteAsyncWithRetrievedTrainings()
    {
        var trainer = new TrainerBuilder().BuildValid();
        var domainEvent = new TrainerDeletedDomainEvent(trainer);
        var trainings = new List<Training>();
        _trainingRepository
            .Setup(r => r.GetByTrainerIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainings);

        var sut = CreateSut();
        await sut.Handle(domainEvent, CancellationToken.None);

        _trainingRepository.Verify(r => r.Delete(trainings), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTrainingsFound_StillCallsDeleteAsync()
    {
        var trainer = new TrainerBuilder().BuildValid();
        var domainEvent = new TrainerDeletedDomainEvent(trainer);
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
