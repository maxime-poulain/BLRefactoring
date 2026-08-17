using AwesomeAssertions;
using TrainingHub.Shared.Common;
using TrainingHub.DDD.Application.Tests.Helpers;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services.TrainerServices;

/// <summary>
/// Behavior covered for <c>TrainerApplicationService</c>.
/// </summary>
public sealed class TrainerApplicationServiceTests
{
    private readonly TrainerServiceTestFixture _fixture = new();

    // -- CreateAsync --

    /// <summary>
    /// Create async, valid request, returns success with dto.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessWithDto()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(request);

        var dto = result.ShouldBeSuccess();
        dto.Firstname.Should().Be("John");
        dto.Lastname.Should().Be("Doe");
        dto.ContactEmail.Should().Be("john.doe@example.com");
    }

    /// <summary>
    /// Create async, valid request, adds trainer and commits once.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidRequest_AddsTrainerAndCommitsOnce()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(request);

        _fixture.TrainerRepository.Verify(r => r.Add(It.IsAny<Trainer>()), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Create async, invalid request, returns failure.
    /// </summary>
    [Fact]
    public async Task CreateAsync_InvalidRequest_ReturnsFailure()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "J", // too short
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(request);

        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create async, invalid request, does not add nor commit.
    /// </summary>
    [Fact]
    public async Task CreateAsync_InvalidRequest_DoesNotAddNorCommit()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "J",
            Lastname = "Doe",
            ContactEmail = "invalid-email",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(request);

        _fixture.TrainerRepository.Verify(r => r.Add(It.IsAny<Trainer>()), Times.Never);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -- EditAsync --

    /// <summary>
    /// Edit async, existing trainer, returns success with updated dto.
    /// </summary>
    [Fact]
    public async Task EditAsync_ExistingTrainer_ReturnsSuccessWithUpdatedDto()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(
            firstname: "Jane",
            lastname: "Smith",
            contactEmail: "jane.smith@example.com",
            bio: "Rewritten bio."), trainer.RowVersion);

        var dto = result.ShouldBeSuccess();
        dto.Firstname.Should().Be("Jane");
        dto.Lastname.Should().Be("Smith");
        dto.ContactEmail.Should().Be("jane.smith@example.com");
        dto.Bio.Should().Be("Rewritten bio.");
    }

    /// <summary>
    /// Edit async, existing trainer, updates trainer and commits once.
    /// </summary>
    [Fact]
    public async Task EditAsync_ExistingTrainer_UpdatesTrainerAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        await sut.EditAsync(EditionRequest(firstname: "Jane"), trainer.RowVersion);

        _fixture.TrainerRepository.Verify(r => r.Update(trainer), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Edit async, null bio, clears the bio.
    /// </summary>
    [Fact]
    public async Task EditAsync_NullBio_ClearsTheBio()
    {
        var trainer = new TrainerBuilder().WithBio("A bio to clear.").Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(bio: null), trainer.RowVersion);

        result.ShouldBeSuccess().Bio.Should().BeNull();
    }

    /// <summary>
    /// Edit async, non existing trainer, returns not found failure.
    /// </summary>
    [Fact]
    public async Task EditAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(), []);

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Edit async, invalid request, returns failure without committing.
    /// </summary>
    [Fact]
    public async Task EditAsync_InvalidRequest_ReturnsFailureWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(contactEmail: "invalid-email"), trainer.RowVersion);

        result.ShouldBeFailure();
        _fixture.TrainerRepository.Verify(r => r.Update(It.IsAny<Trainer>()), Times.Never);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Edit async, stale version, returns concurrency conflict without committing.
    /// </summary>
    [Fact]
    public async Task EditAsync_StaleVersion_ReturnsConcurrencyConflictWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(firstname: "Jane"), [1, 2, 3, 4, 5, 6, 7, 8]);

        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Edit async, store reports a conflict, returns concurrency conflict.
    /// </summary>
    [Fact]
    public async Task EditAsync_StoreReportsAConflict_ReturnsConcurrencyConflict()
    {
        // The pre-check passed, but another request won the race before the update
        // reached the row: the concurrency token is the authoritative guard, and
        // both paths must surface the same business failure.
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _fixture.UnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new InvalidOperationException("inner")));
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(EditionRequest(firstname: "Jane"), trainer.RowVersion);

        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
    }

    private static TrainerEditionRequest EditionRequest(
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer with 10 years of experience.")
        => new()
        {
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };

    // -- GetByIdAsync --

    /// <summary>
    /// Get by id async, existing trainer, returns success with dto.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ExistingTrainer_ReturnsSuccessWithDto()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(trainer.Id.Value);

        var dto = result.ShouldBeSuccess();
        dto.Id.Should().Be(trainer.Id.Value);
    }

    /// <summary>
    /// Get by id async, non existing trainer, returns not found failure.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
    }

    // -- EraseCurrentTrainerAsync --

    /// <summary>
    /// Erase async, marks the caller for deletion, stages the delete and commits once.
    /// </summary>
    [Fact]
    public async Task EraseCurrentTrainerAsync_MarksTheCallerForDeletion_StagesTheDeleteAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.GivenCaller(trainer.Id.Value);
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(trainer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EraseCurrentTrainerAsync();

        result.ShouldBeSuccess();
        trainer.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is TrainerDeletedDomainEvent,
            "the cascade and the portrait's collector both hang off the announcement (ADR 0085)");
        _fixture.TrainerRepository.Verify(r => r.Delete(trainer), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Erase async, caller already gone, returns not found and deletes nothing.
    /// </summary>
    [Fact]
    public async Task EraseCurrentTrainerAsync_CallerAlreadyGone_ReturnsNotFoundAndDeletesNothing()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.EraseCurrentTrainerAsync();

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
        _fixture.TrainerRepository.Verify(r => r.Delete(It.IsAny<Trainer>()), Times.Never);
    }
}
