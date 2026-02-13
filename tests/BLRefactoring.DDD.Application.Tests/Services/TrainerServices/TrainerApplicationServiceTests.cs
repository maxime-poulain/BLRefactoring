using BLRefactoring.DDD.Application.Tests.Helpers;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
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
            Email = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(request);

        var dto = result.ShouldBeSuccess();
        dto.Firstname.Should().Be("John");
        dto.Lastname.Should().Be("Doe");
        dto.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CallsSaveAsync()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "John",
            Lastname = "Doe",
            Email = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(request);

        _fixture.TrainerRepository.Verify(
            r => r.SaveAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ReturnsFailure()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "J", // too short
            Lastname = "Doe",
            Email = "john.doe@example.com",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(request);

        result.ShouldBeFailure();
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_DoesNotCallSaveAsync()
    {
        var request = new TrainerCreationRequest
        {
            Firstname = "J",
            Lastname = "Doe",
            Email = "invalid-email",
            Bio = "Experienced trainer",
            UserId = Guid.NewGuid()
        };
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(request);

        _fixture.TrainerRepository.Verify(
            r => r.SaveAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -- GetByIdAsync --

    [Fact]
    public async Task GetByIdAsync_ExistingTrainer_ReturnsSuccessWithDto()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(trainer.Id);

        var dto = result.ShouldBeSuccess();
        dto.Id.Should().Be((Guid)trainer.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
        var t1 = new TrainerBuilder().WithEmail("a@a.com").BuildValid();
        var t2 = new TrainerBuilder().WithEmail("b@b.com").BuildValid();
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

    // -- DeleteAsync --

    [Fact]
    public async Task DeleteAsync_ExistingTrainer_ReturnsSuccess()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(trainer.Id);

        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrainer_CallsBeginTransactionAsync()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(trainer.Id);

        _fixture.TransactionManager.Verify(
            tm => tm.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrainer_CallsDeleteAsync()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(trainer.Id);

        _fixture.TrainerRepository.Verify(
            r => r.DeleteAsync(trainer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrainer_CallsCommitAsync()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(trainer.Id);

        _fixture.TransactionManager.Verify(
            tm => tm.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingTrainer_ReturnsNotFoundFailure()
    {
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.ShouldContainError(ErrorCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_ReturnsFailure()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _fixture.TrainerRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(trainer.Id);

        result.ShouldBeFailure();
    }

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_CallsRollBack()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _fixture.TrainerRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(trainer.Id);

        _fixture.TransactionManager.Verify(
            tm => tm.RollBackAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_DoesNotCallCommit()
    {
        var trainer = new TrainerBuilder().BuildValid();
        _fixture.TrainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
        _fixture.TrainerRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Trainer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(trainer.Id);

        _fixture.TransactionManager.Verify(
            tm => tm.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
