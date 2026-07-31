using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.DDD.Application.Tests.Helpers;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Services.TrainerServices;

public class TrainerApplicationServiceTests
{
    private readonly TrainerServiceTestFixture _fixture = new();

    // -- CreateAsync --

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

    [Fact]
    public async Task EditAsync_ExistingTrainer_ReturnsSuccessWithUpdatedDto()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(trainer.Id.Value, EditionRequest(
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

    [Fact]
    public async Task EditAsync_ExistingTrainer_UpdatesTrainerAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        await sut.EditAsync(trainer.Id.Value, EditionRequest(firstname: "Jane"), trainer.RowVersion);

        _fixture.TrainerRepository.Verify(r => r.Update(trainer), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_NullBio_ClearsTheBio()
    {
        var trainer = new TrainerBuilder().WithBio("A bio to clear.").Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(trainer.Id.Value, EditionRequest(bio: null), trainer.RowVersion);

        result.ShouldBeSuccess().Bio.Should().BeNull();
    }

    [Fact]
    public async Task EditAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(Guid.NewGuid(), EditionRequest(), []);

        result.ShouldContainError(ErrorCode.NotFound);
    }

    [Fact]
    public async Task EditAsync_InvalidRequest_ReturnsFailureWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(trainer.Id.Value, EditionRequest(contactEmail: "invalid-email"), trainer.RowVersion);

        result.ShouldBeFailure();
        _fixture.TrainerRepository.Verify(r => r.Update(It.IsAny<Trainer>()), Times.Never);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EditAsync_StaleVersion_ReturnsConcurrencyConflictWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(
            trainer.Id.Value, EditionRequest(firstname: "Jane"), [1, 2, 3, 4, 5, 6, 7, 8]);

        result.ShouldContainError(ErrorCode.ConcurrencyConflict);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

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
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new Exception()));
        var sut = _fixture.CreateSut();

        var result = await sut.EditAsync(
            trainer.Id.Value, EditionRequest(firstname: "Jane"), trainer.RowVersion);

        result.ShouldContainError(ErrorCode.ConcurrencyConflict);
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

    [Fact]
    public async Task GetByIdAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCode.NotFound);
    }

    // -- GetAllAsync --

    [Fact]
    public async Task GetAllAsync_ReturnsAllTrainerDtos()
    {
        var t1 = new TrainerBuilder().WithContactEmail("a@a.com").Build();
        var t2 = new TrainerBuilder().WithContactEmail("b@b.com").Build();
        _fixture.TrainerRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([t1, t2]);
        var sut = _fixture.CreateSut();

        var result = await sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_EmptyRepository_ReturnsEmptyArray()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = _fixture.CreateSut();

        var result = await sut.GetAllAsync();

        result.Should().BeEmpty();
    }
}
