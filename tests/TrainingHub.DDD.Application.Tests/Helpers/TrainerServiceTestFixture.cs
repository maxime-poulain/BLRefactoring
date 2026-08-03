using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.Shared;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using Moq;

namespace TrainingHub.DDD.Application.Tests.Helpers;

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
    /// Where the bytes of a photo would go.
    /// </summary>
    /// <remarks>
    /// A mock rather than a fake with storage behind it: the tests here are about the order the
    /// service does things in — bytes written before the row is committed, displaced bytes deleted
    /// only after — and that order is stated by verifying calls on this, not by looking in a
    /// bucket. The round trip is the integration suite's job.
    /// </remarks>
    public Mock<ITrainerPhotoStore> PhotoStore { get; } = new();

    /// <summary>
    /// Create sut.
    /// </summary>
    public TrainerApplicationService CreateSut() => new(
        TrainerRepository.Object,
        PhotoStore.Object,
        CurrentUserService.Object,
        UnitOfWork.Object);
}
