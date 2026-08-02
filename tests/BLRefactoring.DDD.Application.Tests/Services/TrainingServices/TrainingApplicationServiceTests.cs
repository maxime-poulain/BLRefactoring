using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.DDD.Application.Tests.Helpers;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.Services.TrainingServices;

public class TrainingApplicationServiceTests
{
    private readonly TrainingServiceTestFixture _fixture = new();
    private readonly Guid _trainerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private void SetupCurrentUser()
    {
        _fixture.CurrentUserService.Setup(c => c.TrainerId).Returns(_trainerId);
        _fixture.CurrentUserService.Setup(c => c.UserId).Returns(_userId);
    }

    private void SetupTrainerExists()
    {
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupTitleUnique()
    {
        _fixture.TitleChecker
            .Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static TrainingCreationRequest ValidCreationRequest() => new()
    {
        Title = "Valid Training Title",
        Description = "A valid training description content",
        Prerequisites = "Basic programming knowledge required",
        AcquiredSkills = "Advanced design patterns mastery",
        Topics = ["Programming"]
    };

    // -- CreateAsync --

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessWithDto()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(ValidCreationRequest());

        var dto = result.ShouldBeSuccess();
        dto.Title.Should().Be("Valid Training Title");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_AddsTrainingAndCommitsOnce()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(ValidCreationRequest());

        _fixture.TrainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TrainerNotFound_ReturnsNotFoundFailure()
    {
        SetupCurrentUser();
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(ValidCreationRequest());

        result.ShouldContainError(ErrorCode.NotFound);
    }

    [Fact]
    public async Task CreateAsync_InvalidTrainingData_ReturnsFailure()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        var sut = _fixture.CreateSut();

        var request = new TrainingCreationRequest
        {
            Title = "ab", // too short
            Description = "A valid description content here",
            Prerequisites = "Some prerequisites here",
            AcquiredSkills = "Some skills acquired here",
            Topics = ["Programming"]
        };

        var result = await sut.CreateAsync(request);

        result.ShouldBeFailure();
    }

    [Fact]
    public async Task CreateAsync_InvalidTrainingData_DoesNotAddNorCommit()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        var sut = _fixture.CreateSut();

        var request = new TrainingCreationRequest
        {
            Title = "ab",
            Description = "A valid description content here",
            Prerequisites = "Some prerequisites here",
            AcquiredSkills = "Some skills acquired here",
            Topics = ["Programming"]
        };

        await sut.CreateAsync(request);

        _fixture.TrainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Never);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UsesCurrentUserServiceTrainerId()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        var sut = _fixture.CreateSut();

        await sut.CreateAsync(ValidCreationRequest());

        _fixture.TrainerRepository.Verify(
            r => r.ExistsAsync(TrainerId.Create(_trainerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -- GetByIdAsync --

    [Fact]
    public async Task GetByIdAsync_ExistingTraining_ReturnsSuccessWithDto()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(training.Id.Value);

        result.ShouldBeSuccess().Id.Should().Be(training.Id.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCode.NotFound);
    }

    // -- GetAllAsync --

    [Fact]
    public async Task GetAllAsync_ReturnsAllTrainingDtos()
    {
        var t1 = await new TrainingBuilder().WithTitle("Training One Title").BuildValidAsync();
        var t2 = await new TrainingBuilder().WithTitle("Training Two Title").BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([t1, t2]);
        var sut = _fixture.CreateSut();

        var result = await sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_EmptyRepository_ReturnsEmptyList()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = _fixture.CreateSut();

        var result = await sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    // -- EditAsync --

    [Fact]
    public async Task EditAsync_ValidRequest_ReturnsSuccessWithDto()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.TitleChecker
            .Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = _fixture.CreateSut();

        var request = new TrainingEditionRequest
        {
            Title = "Updated Title Here",
            Description = "Updated description content here",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Design"]
        };

        var result = await sut.EditAsync(training.Id.Value, request, training.RowVersion);

        result.ShouldBeSuccess().Title.Should().Be("Updated Title Here");
    }

    [Fact]
    public async Task EditAsync_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var request = new TrainingEditionRequest
        {
            Title = "Updated Title Here",
            Description = "Updated description content",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated skills content",
            Topics = ["Design"]
        };

        var result = await sut.EditAsync(Guid.NewGuid(), request, []);

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCode.NotFound);
    }

    [Fact]
    public async Task EditAsync_StaleVersion_ReturnsConcurrencyConflictWithoutCommitting()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var request = new TrainingEditionRequest
        {
            Title = "Updated Title Here",
            Description = "Updated description content here",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Design"]
        };

        var result = await sut.EditAsync(training.Id.Value, request, [1, 2, 3, 4, 5, 6, 7, 8]);

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
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.UnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new Exception()));
        var sut = _fixture.CreateSut();

        var request = new TrainingEditionRequest
        {
            Title = "Updated Title Here",
            Description = "Updated description content here",
            Prerequisites = "Updated prerequisites content",
            AcquiredSkills = "Updated acquired skills content",
            Topics = ["Design"]
        };

        var result = await sut.EditAsync(training.Id.Value, request, training.RowVersion);

        result.ShouldContainError(ErrorCode.ConcurrencyConflict);
    }

    [Fact]
    public async Task EditAsync_InvalidData_ReturnsFailure()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var request = new TrainingEditionRequest
        {
            Title = "ab", // too short
            Description = "Valid description content here",
            Prerequisites = "Valid prerequisites content",
            AcquiredSkills = "Valid acquired skills here",
            Topics = ["Programming"]
        };

        var result = await sut.EditAsync(training.Id.Value, request, training.RowVersion);

        result.ShouldBeFailure();
    }

    // -- GetByTrainerIdAsync --

    [Fact]
    public async Task GetByTrainerIdAsync_ReturnsTrainingDtos()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByTrainerIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Training> { training });
        var sut = _fixture.CreateSut();

        var trainings = await sut.GetByTrainerIdAsync(Guid.NewGuid());

        trainings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByTrainerIdAsync_NoTrainings_ReturnsAnEmptyList()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByTrainerIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Training>());
        var sut = _fixture.CreateSut();

        var trainings = await sut.GetByTrainerIdAsync(Guid.NewGuid());

        // Empty rather than a failure: a trainer who has created nothing is not an error, which
        // is why this method stopped answering with a Result it could never fail.
        trainings.Should().BeEmpty();
    }

    // -- DeleteAsync --

    [Fact]
    public async Task DeleteAsync_ExistingTraining_ReturnsSuccess()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(training.Id.Value);

        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task DeleteAsync_ExistingTraining_DeletesTrainingAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        await sut.DeleteAsync(training.Id.Value);

        _fixture.TrainingRepository.Verify(r => r.Delete(training), Times.Once);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.ShouldContainError(ErrorCode.NotFound);
    }
}
