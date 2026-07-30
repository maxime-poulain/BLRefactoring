using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Moq;

namespace BLRefactoring.Shared.Domain.Tests.Helpers;

public class TrainingBuilder
{
    private string _title = "Valid Training Title";
    private string _description = "A valid training description for testing purposes";
    private string _prerequisites = "Basic programming knowledge required";
    private string _acquiredSkills = "Advanced design patterns mastery";
    private Guid _trainerId = Guid.NewGuid();
    private List<string> _topics = ["Programming"];
    private bool _titleExistsResult;

    public TrainingBuilder WithTitle(string v) { _title = v; return this; }
    public TrainingBuilder WithDescription(string v) { _description = v; return this; }
    public TrainingBuilder WithPrerequisites(string v) { _prerequisites = v; return this; }
    public TrainingBuilder WithAcquiredSkills(string v) { _acquiredSkills = v; return this; }
    public TrainingBuilder WithTrainerId(Guid v) { _trainerId = v; return this; }
    public TrainingBuilder WithTopics(params string[] v) { _topics = v.ToList(); return this; }
    public TrainingBuilder WithTitleAlreadyExists() { _titleExistsResult = true; return this; }

    public Mock<IUniquenessTitleChecker> CreateTitleCheckerMock()
    {
        var mock = new Mock<IUniquenessTitleChecker>();
        mock.Setup(c => c.TitleForTrainerExists(
                It.IsAny<TrainingTitle>(),
                It.IsAny<TrainerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_titleExistsResult);
        return mock;
    }

    public async Task<Result<Training>> BuildAsync()
    {
        var mockChecker = CreateTitleCheckerMock();

        return await Training.CreateAsync(
            new TrainingCreationMessage
            {
                TrainingId = TrainingId.Generate(),
                Title = _title,
                Description = _description,
                Prerequisites = _prerequisites,
                AcquiredSkills = _acquiredSkills,
                TrainerId = TrainerId.Create(_trainerId),
                Topics = _topics
            },
            mockChecker.Object);
    }

    public async Task<Training> BuildValidAsync()
    {
        return (await BuildAsync()).ShouldBeSuccess();
    }
}
