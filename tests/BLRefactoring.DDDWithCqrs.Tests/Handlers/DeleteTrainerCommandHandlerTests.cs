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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DeleteTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ExistingTrainer_ReturnsSuccess()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainerCommand(trainer.Id.Value), CancellationToken.None);

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
    public async Task Handle_ExistingTrainer_DeletesTrainerAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = CreateSut();

        await sut.Handle(new DeleteTrainerCommand(trainer.Id.Value), CancellationToken.None);

        _trainerRepository.Verify(r => r.Delete(trainer), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SaveChangesThrows_Rethrows()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _unitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        Func<Task> act = async () => await sut.Handle(new DeleteTrainerCommand(trainer.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("DB error");
    }
}
