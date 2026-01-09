using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

public class TrainingEditionMessage
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
    public required IEnumerable<string> Topics { get; init; }
}

public class TrainingCreationMessage : TrainingEditionMessage
{
    public required Guid TrainerId { get; init; }
    public required Guid UserId { get; init; }
}

public sealed class Training : AggregateRoot<TrainingId>
{
    private readonly List<Topic> _topics = [];
    public IReadOnlyCollection<Topic> Topics => _topics.AsReadOnly();

    public TrainingTitle Title { get; private set; } = null!;

    public AcquiredSkills AcquiredSkills { get; private set; } = null!;

    public TrainingDescription Description { get; private set; } = null!;

    public TrainingPrerequisites Prerequisites { get; private set; } = null!;

    public TrainerId TrainerId { get; private set; } = null!;

    private Training()
    {
    } // Private constructor for ORM or serialization

    private Training(TrainingId trainingId, TrainerId trainerId)
    {
        Id = trainingId;
        TrainerId = trainerId;
    }

    private static Training CreateDraft(TrainingId trainingId, TrainerId trainerId)
        => new(trainingId, trainerId);

    // Usually factory methods are implemented in a separate Factory class.
    // a `TrainingFactory` class would be responsible for creating Training objects.
    // This is a simplified version of a factory method. Furthermore the logic to build
    // the object is not complex enough to justify a separate class.
    // Refactoring to a factory class would not be complicated though.
    // It might have been if we were using the constructor to build the object.
    // Another good practice is to pass in an request(or message) object.
    // This encapsulates parameters and makes the code more readable.
    // Furthermore, it makes it easier to add new parameters in the future and decrease
    // the number of parameters in the method.
    public static async Task<Result<Training>> CreateAsync(
        TrainingCreationMessage trainingCreationMessage,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainingCreationMessage);
        ArgumentNullException.ThrowIfNull(titleChecker);

        var training = CreateDraft(
            TrainingId.Create(Guid.NewGuid()),
            (TrainerId)trainingCreationMessage.TrainerId);

        await training.EditAsync(trainingCreationMessage, titleChecker, cancellationToken);
        return Result<Training>.Success(training);
    }

    public async Task<Result> EditAsync(TrainingEditionMessage message,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(titleChecker);

        var errors = new ErrorCollection();

        var titleResult = TrainingTitle.Create(message.Title);
        titleResult.Switch(
            onSuccess: _ => { },
            onFailure: errs => errors.AddErrors(errs));

        if (!titleResult.HasErrors())
        {
            var title = titleResult.Match(trainingTitle => trainingTitle, _ => null!);
            if (title != Title)
            {
                var titleExists = await titleChecker.TitleForTrainerExists(title, TrainerId, cancellationToken);
                if (titleExists)
                {
                    errors.Add(new Error(ErrorCode.DuplicateTitle,
                        "A training with the same title already exists for this trainer."));
                }
            }
        }

        var descriptionResult = TrainingDescription.Create(message.Description);
        descriptionResult.Switch(
            onSuccess: _ => { },
            onFailure: errs => errors.AddErrors(errs));

        var prerequisitesResult = TrainingPrerequisites.Create(message.Prerequisites);
        prerequisitesResult.Switch(
            onSuccess: _ => { },
            onFailure: errs => errors.AddErrors(errs));

        var acquiredSkillsResult = AcquiredSkills.Create(message.AcquiredSkills);
        acquiredSkillsResult.Switch(
            onSuccess: _ => { },
            onFailure: errs => errors.AddErrors(errs));

        if (errors.Any())
        {
            return Result.Failure(errors);
        }

        titleResult.Switch(
            onSuccess: title => Title = title,
            onFailure: _ => { });

        descriptionResult.Switch(
            onSuccess: desc => Description = desc,
            onFailure: _ => { });

        prerequisitesResult.Switch(
            onSuccess: prereq => Prerequisites = prereq,
            onFailure: _ => { });

        acquiredSkillsResult.Switch(
            onSuccess: skills => AcquiredSkills = skills,
            onFailure: _ => { });

        _topics.Clear();
        _topics.AddRange(message.Topics.Select(Topic.FromName));

        AddDomainEvent(new TrainingEditedDomainEvent(this));

        return Result.Success();
    }
}
