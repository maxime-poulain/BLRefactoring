using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Erase;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for <see cref="EraseCurrentTrainerCommandHandler"/>.
/// </summary>
public sealed class EraseCurrentTrainerCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EraseCurrentTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _currentUserService.Object, _unitOfWork.Object);

    /// <summary>
    /// Handle, marks the caller for deletion, stages the delete and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_MarksTheCallerForDeletion_StagesTheDeleteAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        _currentUserService.SetupGet(service => service.TrainerId).Returns(trainer.Id.Value);
        _trainerRepository
            .Setup(r => r.GetByIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);

        var result = await CreateSut().Handle(new EraseCurrentTrainerCommand(), CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is TrainerDeletedDomainEvent,
            "the cascade and the portrait's collector both hang off the announcement (ADR 0085)");
        _trainerRepository.Verify(r => r.Delete(trainer), Times.Once);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Handle, caller already gone, returns not found and deletes nothing.
    /// </summary>
    [Fact]
    public async Task Handle_CallerAlreadyGone_ReturnsNotFoundAndDeletesNothing()
    {
        _currentUserService.SetupGet(service => service.TrainerId).Returns(Guid.NewGuid());
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);

        var result = await CreateSut().Handle(new EraseCurrentTrainerCommand(), CancellationToken.None);

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
        _trainerRepository.Verify(r => r.Delete(It.IsAny<Trainer>()), Times.Never);
    }
}
