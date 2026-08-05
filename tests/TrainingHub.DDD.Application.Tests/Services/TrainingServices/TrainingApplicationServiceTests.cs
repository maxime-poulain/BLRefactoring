using AwesomeAssertions;
using TrainingHub.Shared.Common;
using TrainingHub.DDD.Application.Tests.Helpers;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services.TrainingServices;

/// <summary>
/// Behaviour covered for <c>TrainingApplicationService</c>.
/// </summary>
public sealed class TrainingApplicationServiceTests
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
            .Setup(c => c.TitleForTrainerExistsAsync(
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

    /// <summary>
    /// Create async, valid request, returns success with dto.
    /// </summary>
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

    /// <summary>
    /// Create async, valid request, adds training and commits once.
    /// </summary>
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

    /// <summary>
    /// Create async, trainer not found, returns not found failure.
    /// </summary>
    [Fact]
    public async Task CreateAsync_TrainerNotFound_ReturnsNotFoundFailure()
    {
        SetupCurrentUser();
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(ValidCreationRequest());

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Create async, the catalogue is full, surfaces the domain's refusal and does not save.
    /// </summary>
    /// <remarks>
    /// The rule and its message are the aggregate's, proven in the domain suite; what this
    /// stack owes is to hand the factory the counter and to let the refusal through untouched.
    /// </remarks>
    [Fact]
    public async Task CreateAsync_CatalogueFull_SurfacesTheRefusalAndDoesNotSave()
    {
        SetupCurrentUser();
        SetupTrainerExists();
        SetupTitleUnique();
        _fixture.TrainingCounter
            .Setup(c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Training.MaximumPerTrainer);
        var sut = _fixture.CreateSut();

        var result = await sut.CreateAsync(ValidCreationRequest());

        result.ShouldContainError(TrainingErrorCodes.CatalogueFull);
        _fixture.TrainingRepository.Verify(r => r.Add(It.IsAny<Training>()), Times.Never);
        _fixture.UnitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Create async, invalid training data, returns failure.
    /// </summary>
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

    /// <summary>
    /// Create async, invalid training data, does not add nor commit.
    /// </summary>
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

    /// <summary>
    /// Create async, uses current user service trainer id.
    /// </summary>
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

    /// <summary>
    /// Get by id async, own training, returns success with dto.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_OwnTraining_ReturnsSuccessWithDto()
    {
        SetupCurrentUser();
        var training = await new TrainingBuilder().WithTrainerId(_trainerId).BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(training.Id.Value);

        result.ShouldBeSuccess().Id.Should().Be(training.Id.Value);
    }

    /// <summary>
    /// Get by id async, non existing training, returns not found failure.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_NonExistingTraining_ReturnsNotFoundFailure()
    {
        SetupCurrentUser();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
    }

    /// <summary>
    /// Get by id async, another trainers training, returns the same not found failure.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_AnotherTrainersTraining_ReturnsTheSameNotFoundFailure()
    {
        SetupCurrentUser();
        var training = await new TrainingBuilder().WithTrainerId(Guid.NewGuid()).BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        var sut = _fixture.CreateSut();

        var result = await sut.GetByIdAsync(training.Id.Value);

        // The same failure as an identifier naming nothing, deliberately: a distinct one would
        // tell the caller that this training exists, which is the fact being withheld. The read
        // was found and refused, and the answer says only the second part.
        var errors = result.ShouldBeFailure();
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
    }

    // -- EditAsync --

    /// <summary>
    /// Edit async, valid request, returns success with dto.
    /// </summary>
    [Fact]
    public async Task EditAsync_ValidRequest_ReturnsSuccessWithDto()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.TitleChecker
            .Setup(c => c.TitleForTrainerExistsAsync(
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

    /// <summary>
    /// Edit async, non existing training, returns not found failure.
    /// </summary>
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
        errors.Should().Contain(e => e.ErrorCode == ErrorCodes.NotFound);
    }

    /// <summary>
    /// Edit async, stale version, returns concurrency conflict without committing.
    /// </summary>
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
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.UnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new InvalidOperationException("inner")));
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

        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
    }

    /// <summary>
    /// Edit async, invalid data, returns failure.
    /// </summary>
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

    // -- GetMineAsync --

    /// <summary>
    /// Get mine async, returns the repository's page, items mapped and counts untouched.
    /// </summary>
    [Fact]
    public async Task GetMineAsync_ReturnsThePage_ItemsMappedAndCountsUntouched()
    {
        SetupCurrentUser();
        var training = await new TrainingBuilder().WithTrainerId(_trainerId).BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetPageByTrainerIdAsync(
                It.IsAny<TrainerId>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Training>([training], Page: 2, PageSize: 1, TotalCount: 5));
        var sut = _fixture.CreateSut();

        var page = await sut.GetMineAsync(new PageRequest { Page = 2, PageSize = 1 });

        // The repository decided what the page holds; this layer only reshapes each item. The
        // metadata crossing the mapping unchanged is what keeps the pager honest.
        page.Items.Should().HaveCount(1);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().Be(5);
    }

    /// <summary>
    /// Get mine async, asks the repository for the callers own trainer id and the callers page.
    /// </summary>
    [Fact]
    public async Task GetMineAsync_AsksTheRepositoryForTheCallersOwnTrainerId_AndTheCallersPage()
    {
        SetupCurrentUser();
        _fixture.TrainingRepository
            .Setup(r => r.GetPageByTrainerIdAsync(
                It.IsAny<TrainerId>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Training>([], Page: 3, PageSize: 10, TotalCount: 0));
        var sut = _fixture.CreateSut();

        var paging = new PageRequest { Page = 3, PageSize = 10 };
        await sut.GetMineAsync(paging);

        // The method takes no trainer, so the only thing that can make it read the wrong one is
        // resolving the caller wrongly. This is where that would show — and the paging must reach
        // the repository as given, or the caller walks pages the database never served.
        _fixture.TrainingRepository.Verify(
            r => r.GetPageByTrainerIdAsync(
                TrainerId.Create(_trainerId), paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Get mine async, no trainings, returns an empty page.
    /// </summary>
    [Fact]
    public async Task GetMineAsync_NoTrainings_ReturnsAnEmptyPage()
    {
        SetupCurrentUser();
        _fixture.TrainingRepository
            .Setup(r => r.GetPageByTrainerIdAsync(
                It.IsAny<TrainerId>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Training>([], Page: 1, PageSize: PageRequest.DefaultPageSize, TotalCount: 0));
        var sut = _fixture.CreateSut();

        var page = await sut.GetMineAsync(new PageRequest());

        // Empty rather than a failure: a trainer who has created nothing is not an error, which
        // is why this method stopped answering with a Result it could never fail.
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    // -- DeleteAsync --

    /// <summary>
    /// Delete async, existing training, returns success.
    /// </summary>
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

    /// <summary>
    /// Delete async, existing training, deletes training and commits once.
    /// </summary>
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

    /// <summary>
    /// Delete async, non existing training, returns not found failure.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_NonExistingTraining_ReturnsNotFoundFailure()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Transfer async, a willing recipient, reassigns and saves.
    /// </summary>
    [Fact]
    public async Task TransferAsync_AWillingRecipient_ReassignsAndSaves()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        var recipient = Guid.NewGuid();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = _fixture.CreateSut();

        var result = await sut.TransferAsync(training.Id.Value, recipient);

        result.ShouldBeSuccess();
        training.TrainerId.Value.Should().Be(recipient);
        _fixture.TrainingRepository.Verify(r => r.Update(training), Times.Once);
        _fixture.UnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Transfer async, a non existing training, returns not found.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ANonExistingTraining_ReturnsNotFound()
    {
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);
        var sut = _fixture.CreateSut();

        var result = await sut.TransferAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Transfer async, a ghost recipient, is refused before the domain service is consulted.
    /// </summary>
    [Fact]
    public async Task TransferAsync_AGhostRecipient_IsRefusedBeforeTheServiceIsConsulted()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = _fixture.CreateSut();

        var result = await sut.TransferAsync(training.Id.Value, Guid.NewGuid());

        result.ShouldContainError(TrainingErrorCodes.UnknownRecipient);
        _fixture.TrainingCounter.Verify(
            c => c.CountForTrainerAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fixture.UnitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Transfer async, a lost uniqueness race at save, answers duplicate title.
    /// </summary>
    [Fact]
    public async Task TransferAsync_ALostUniquenessRaceAtSave_AnswersDuplicateTitle()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        _fixture.TrainingRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(training);
        _fixture.TrainerRepository
            .Setup(r => r.ExistsAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fixture.UnitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintViolationException("duplicate", new InvalidOperationException()));
        var sut = _fixture.CreateSut();

        var result = await sut.TransferAsync(training.Id.Value, Guid.NewGuid());

        result.ShouldContainError(TrainingErrorCodes.DuplicateTitle);
    }
}
