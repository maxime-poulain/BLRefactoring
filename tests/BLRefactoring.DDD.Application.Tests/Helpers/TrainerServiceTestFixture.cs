using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Moq;

namespace BLRefactoring.DDD.Application.Tests.Helpers;

/// <summary>
/// Trainer service test fixture.
/// </summary>
public sealed class TrainerServiceTestFixture
{
    /// <summary>
    /// Trainer repository.
    /// </summary>
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();

    /// <summary>
    /// Current user service.
    /// </summary>
    public Mock<ICurrentUserService> CurrentUserService { get; } = new();

    /// <summary>
    /// Unit of work.
    /// </summary>
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    /// <summary>
    /// Who the service will take the caller to be. Non-empty by default, because
    /// <c>TrainerId.Create</c> refuses <c>Guid.Empty</c> and a bare mock would throw before a test
    /// reached its assertion.
    /// </summary>
    public Guid CallerId { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Trainer service test fixture.
    /// </summary>
    public TrainerServiceTestFixture() => GivenCaller(CallerId);

    /// <summary>Makes the service resolve <paramref name="trainerId"/> as the calling trainer.</summary>
    /// <remarks>
    /// EditAsync no longer takes the trainer it edits — it serves <c>PUT /Trainer/me</c> and reads
    /// the caller itself — so a test states who is calling instead of passing an identifier.
    /// </remarks>
    public void GivenCaller(Guid trainerId)
    {
        CallerId = trainerId;
        CurrentUserService.SetupGet(service => service.TrainerId).Returns(trainerId);
    }

    /// <summary>
    /// Create sut.
    /// </summary>
    public TrainerApplicationService CreateSut() => new(
        TrainerRepository.Object,
        CurrentUserService.Object,
        UnitOfWork.Object);
}
