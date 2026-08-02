using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Moq;

namespace BLRefactoring.DDD.Application.Tests.Helpers;

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
        TrainingRepository.Object,
        CurrentUserService.Object,
        UnitOfWork.Object);
}
