using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Delete;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>DeleteTrainingCommandHandler</c>.
/// </summary>
public sealed class DeleteTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DeleteTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _unitOfWork.Object);

    /// <summary>
    /// Handle, existing training, returns success.
    /// </summary>
    [Fact]
    public async Task Handle_ExistingTraining_ReturnsSuccess()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainingCommand(training.Id.Value), CancellationToken.None);

        result.ShouldBeSuccess();
        _trainingRepository.Verify(r => r.Delete(training), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, non existing training, returns not found failure.
    /// </summary>
    [Fact]
    public async Task Handle_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainingCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Handle, existing training, raises the deletion fact before staging the removal.
    /// </summary>
    /// <remarks>
    /// Deleting used to raise nothing at all, which is why a deleted training stayed in the search
    /// index for ever: the row went, and no consumer was ever told (ADR 0050).
    /// </remarks>
    [Fact]
    public async Task Handle_ExistingTraining_RaisesTheDeletionFact()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.ClearDomainEvents();
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);

        await CreateSut().Handle(new DeleteTrainingCommand(training.Id.Value), CancellationToken.None);

        training.DomainEvents.Should().ContainSingle(e => e is TrainingDeletedDomainEvent);
    }
}
