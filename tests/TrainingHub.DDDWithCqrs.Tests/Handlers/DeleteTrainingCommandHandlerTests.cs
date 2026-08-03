using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Delete;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
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
}
