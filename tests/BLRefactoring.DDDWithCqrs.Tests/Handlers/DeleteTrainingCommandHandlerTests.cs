using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public class DeleteTrainingCommandHandlerTests
{
    private readonly Mock<ITrainingRepository> _trainingRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DeleteTrainingCommandHandler CreateSut() =>
        new(_trainingRepository.Object, _unitOfWork.Object);

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

    [Fact]
    public async Task Handle_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _trainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = CreateSut();

        var result = await sut.Handle(new DeleteTrainingCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCode.NotFound);
    }
}
