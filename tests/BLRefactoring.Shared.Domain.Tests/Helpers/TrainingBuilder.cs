using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Moq;

namespace BLRefactoring.Shared.Domain.Tests.Helpers;

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

        return await Training.CreateAsync(
            TrainingId.Generate(),
            TrainerId.Create(_trainerId),
            TrainingTitle.Create(_title).ShouldBeSuccess(),
            TrainingDescription.Create(_description).ShouldBeSuccess(),
            TrainingPrerequisites.Create(_prerequisites).ShouldBeSuccess(),
            AcquiredSkills.Create(_acquiredSkills).ShouldBeSuccess(),
            BuildTopics(),
            mockChecker.Object);
    }

    /// <summary>
    /// Build valid async.
    /// </summary>
    public async Task<Training> BuildValidAsync()
    {
        return (await BuildAsync()).ShouldBeSuccess();
    }
}
