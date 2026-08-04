using TrainingHub.DDD.Application.Services.TrainingServices;
using TrainingHub.Shared;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Moq;

namespace TrainingHub.DDD.Application.Tests.Helpers;

/// <summary>
/// Training service test fixture.
/// </summary>
public sealed class TrainingServiceTestFixture
{
    /// <summary>
    /// Trainer repository.
    /// </summary>
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();

    /// <summary>
    /// Title checker.
    /// </summary>
    public Mock<IUniquenessTitleChecker> TitleChecker { get; } = new();

    /// <summary>
    /// Training counter. Answers zero unless a test raises it: an empty catalogue is the
    /// default, so only the tests about the capacity rule mention it.
    /// </summary>
    public Mock<ITrainingCounter> TrainingCounter { get; } = new();

    /// <summary>
    /// Training repository.
    /// </summary>
    public Mock<ITrainingRepository> TrainingRepository { get; } = new();

    /// <summary>
    /// Current user service.
    /// </summary>
    public Mock<ICurrentUserService> CurrentUserService { get; } = new();

    /// <summary>
    /// Unit of work.
    /// </summary>
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    /// <summary>
    /// Create sut.
    /// </summary>
    public TrainingApplicationService CreateSut() => new(
        TrainerRepository.Object,
        TitleChecker.Object,
        TrainingCounter.Object,
        TrainingRepository.Object,
        CurrentUserService.Object,
        UnitOfWork.Object);
}
