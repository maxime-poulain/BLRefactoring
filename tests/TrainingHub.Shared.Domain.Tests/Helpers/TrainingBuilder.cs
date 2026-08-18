using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Moq;

namespace TrainingHub.Shared.Domain.Tests.Helpers;

/// <summary>
/// Builds a training from value objects, the same way the application layer does.
/// Creation still returns a <see cref="Result{T}"/> because title uniqueness is a
/// cross-aggregate rule the aggregate can only settle through the checker.
/// </summary>
public sealed class TrainingBuilder
{
    private string _title = "Valid Training Title";
    private string _description = "A valid training description for testing purposes";
    private string _prerequisites = "Basic programming knowledge required";
    private string _acquiredSkills = "Advanced design patterns mastery";
    private Guid _trainerId = Guid.NewGuid();
    private List<string> _topics = ["Programming"];
    private bool _titleExistsResult;
    private int _publishedCount;
    private bool _trainerSuspended;
    private bool _trainerVerified = true;

    /// <summary>
    /// With title.
    /// </summary>
    public TrainingBuilder WithTitle(string v) { _title = v; return this; }

    /// <summary>
    /// With description.
    /// </summary>
    public TrainingBuilder WithDescription(string v) { _description = v; return this; }

    /// <summary>
    /// With prerequisites.
    /// </summary>
    public TrainingBuilder WithPrerequisites(string v) { _prerequisites = v; return this; }

    /// <summary>
    /// With acquired skills.
    /// </summary>
    public TrainingBuilder WithAcquiredSkills(string v) { _acquiredSkills = v; return this; }

    /// <summary>
    /// With trainer id.
    /// </summary>
    public TrainingBuilder WithTrainerId(Guid v) { _trainerId = v; return this; }

    /// <summary>
    /// With topics.
    /// </summary>
    public TrainingBuilder WithTopics(params string[] v) { _topics = v.ToList(); return this; }

    /// <summary>
    /// With title already exists.
    /// </summary>
    public TrainingBuilder WithTitleAlreadyExists() { _titleExistsResult = true; return this; }

    /// <summary>
    /// With this many trainings already published by the trainer.
    /// </summary>
    public TrainingBuilder WithTrainingsAlreadyPublished(int count) { _publishedCount = count; return this; }

    /// <summary>
    /// Create title checker mock.
    /// </summary>
    public Mock<IUniquenessTitleChecker> CreateTitleCheckerMock()
    {
        var mock = new Mock<IUniquenessTitleChecker>();
        mock.Setup(c => c.TitleForTrainerExistsAsync(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_titleExistsResult);
        return mock;
    }

    /// <summary>
    /// With the owning trainer under sanction (ADR 0050).
    /// </summary>
    public TrainingBuilder WithSuspendedTrainer() { _trainerSuspended = true; return this; }

    /// <summary>
    /// Create trainer standing mock.
    /// </summary>
    public Mock<ITrainerStanding> CreateTrainerStandingMock()
    {
        var mock = new Mock<ITrainerStanding>();
        mock.Setup(c => c.IsSuspendedAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_trainerSuspended);
        return mock;
    }

    /// <summary>
    /// With the owning trainer's account still unverified (ADR 0090).
    /// </summary>
    public TrainingBuilder WithUnverifiedTrainer() { _trainerVerified = false; return this; }

    /// <summary>
    /// Create trainer verification mock.
    /// </summary>
    public Mock<ITrainerVerification> CreateTrainerVerificationMock()
    {
        var mock = new Mock<ITrainerVerification>();
        mock.Setup(c => c.IsVerifiedAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_trainerVerified);
        return mock;
    }

    /// <summary>
    /// Create training counter mock.
    /// </summary>
    public Mock<ITrainingCounter> CreateTrainingCounterMock()
    {
        var mock = new Mock<ITrainingCounter>();
        mock.Setup(c => c.CountForTrainerAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_publishedCount);
        return mock;
    }

    /// <summary>
    /// Build topics.
    /// </summary>
    public IReadOnlyCollection<Topic> BuildTopics()
        => _topics.Select(name => Topic.TryFromName(name, out var topic)
                ? topic
                : throw new ArgumentException($"Topic with name '{name}' does not exist."))
            .ToList();

    /// <summary>
    /// Build async.
    /// </summary>
    public async Task<Result<Training>> BuildAsync()
    {
        var mockChecker = CreateTitleCheckerMock();
        var mockCounter = CreateTrainingCounterMock();
        var mockStanding = CreateTrainerStandingMock();
        var mockVerification = CreateTrainerVerificationMock();

        return await Training.CreateAsync(
            TrainingId.Generate(),
            TrainerId.Create(_trainerId),
            TrainingTitle.Create(_title).ShouldBeSuccess(),
            TrainingDescription.Create(_description).ShouldBeSuccess(),
            TrainingPrerequisites.Create(_prerequisites).ShouldBeSuccess(),
            AcquiredSkills.Create(_acquiredSkills).ShouldBeSuccess(),
            BuildTopics(),
            mockChecker.Object,
            mockCounter.Object,
            mockStanding.Object,
            mockVerification.Object);
    }

    /// <summary>
    /// Build valid async.
    /// </summary>
    public async Task<Training> BuildValidAsync()
    {
        return (await BuildAsync()).ShouldBeSuccess();
    }
}
