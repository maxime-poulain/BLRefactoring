using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public class DeleteTrainerCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ITransactionManager> _transactionManager = new();

    private DeleteTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _transactionManager.Object);

    [Fact]
    public async Task Handle_ExistingTrainer_ReturnsSuccess()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainerCommand(trainer.Id), CancellationToken.None);

        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Handle_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainerCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_ExistingTrainer_UsesTransaction()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        await sut.Handle(new DeleteTrainerCommand(trainer.Id), CancellationToken.None);

        _transactionManager.Verify(
            tm => tm.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionManager.Verify(
            tm => tm.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RepositoryThrows_CallsRollbackAndRethrows()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _trainerRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        Func<Task> act = async () => await sut.Handle(new DeleteTrainerCommand(trainer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("DB error");
        _transactionManager.Verify(
            tm => tm.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionManager.Verify(
            tm => tm.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
