using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

public sealed class Training : AggregateRoot<TrainingId>
{
    private readonly List<Rate> _rates = null!;

    public string Title { get; private set; } = null!;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public Guid TrainerId { get; private set; }

    public IReadOnlyCollection<Rate> Rates => _rates;

    private Training() { } // Private constructor for ORM or serialization

    public Training(TrainingId trainingId)
    {
        Id = trainingId;
        _rates = new List<Rate>();
    }

    private static Training CreateDraft(TrainingId trainingId) => new(trainingId);

    // Usually factory methods are implemented in a separate Factory class.
    // a `TrainingFactory` class would be responsible for creating Training objects.
    // This is a simplified version of a factory method. Furthermore the logic to build
    // the object is not complex enough to justify a separate class.
    // Refactoring to a factory class would not be complicated though.
    // It might have been if we were using the constructor to build the object.
    // Another good practice is to pass in an request(or message) object.
    // This encapsulate parameters and makes the code more readable.
    // Furthermore, it makes it easier to add new parameters in the future and decrease
    // the number of parameters in the method.
    public static async Task<Result<Training>> CreateAsync(
        string title,
        DateTime startDate,
        DateTime endDate,
        Trainer trainer,
        List<Rate> rates,
        IUniquenessTitleChecker titleChecker)
    {
        var training = CreateDraft(TrainingId.Default());
        return await CreateInternalAsync(training,
            title,
            startDate,
            endDate,
            trainer,
            rates,
            titleChecker);
    }

    public static async Task<Result<Training>> CreateAsync(
        TrainingId trainingId,
        string title,
        DateTime startDate,
        DateTime endDate,
        Trainer trainer,
        List<Rate> rates,
        IUniquenessTitleChecker titleChecker)
    {
        var training = CreateDraft(trainingId);
        return await CreateInternalAsync(training,
            title,
            startDate,
            endDate,
            trainer,
            rates,
            titleChecker);
    }

    private static async Task<Result<Training>> CreateInternalAsync(Training training,
        string title,
        DateTime startDate,
        DateTime endDate,
        Trainer trainer,
        List<Rate> rates,
        IUniquenessTitleChecker titleChecker)
    {
        return (await training.ChangeTitleAsync(title, titleChecker, trainer))
            .Bind(() => training.ChangeEndDate(endDate))
            .Bind(() => training.ChangeStartDate(startDate))
            .Bind(() => training.ChangeTrainer(trainer))
            .Bind(() => training.ChangeRates(rates))
            .Bind(() =>
            {
                training.AddDomainEvent(new TrainingCreatedDomainEvent(training));
                return Result.Success();
            })
            .Match(
                onSuccess: () => Result<Training>.Success(training),
                onFailure: Result<Training>.Failure
            );
    }

    public Result ChangeStartDate(DateTime startDate)
    {
        if (startDate <= DateTime.Now)
        {
            return Result.Failure(ErrorCode.InvalidStartDate, "Start date must be in the future.");
        }

        if (startDate > EndDate)
        {
            return Result.Failure(ErrorCode.InvalidStartDate, "Start date must be before end date.");
        }

        StartDate = startDate;
        return Result.Success();
    }

    public Result ChangeEndDate(DateTime endDate)
    {
        if (endDate >= DateTime.Now.AddYears(3))
        {
            return Result.Failure(ErrorCode.InvalidEndDate, "End date must be within 3 years from now.");
        }

        if (endDate < StartDate)
        {
            return Result.Failure(ErrorCode.InvalidEndDate, "End date must be after start date.");
        }

        EndDate = endDate;
        return Result.Success();
    }

    public Result ChangeTrainer(Trainer trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        TrainerId = trainer.Id;
        if (trainer.IsTransient())
        {
            return Result.Failure(ErrorCode.InvalidTrainer, "Invalid new trainer assignment for training");
        }
        TrainerId = trainer.Id;
        return Result.Success();
    }

    public Result ChangeRates(List<Rate> rates)
    {
        if (rates == null)
        {
            return Result.Failure(ErrorCode.InvalidRates, "Rates cannot be null.");
        }
        _rates.Clear();
        _rates.AddRange(rates);
        return Result.Success();
    }

    public async Task<Result> ChangeTitleAsync(string title, IUniquenessTitleChecker checker, Trainer trainer)
    {
        var errors = new ErrorCollection();

        if(title is { Length: <5 or > 30})
        {
            errors.Add(ErrorCode.InvalidTitle, "Title must be between 5 and 30 characters.");
        }

        if (!await checker.IsTitleUniqueAsync(title, trainer))
        {
            errors.Add(ErrorCode.DuplicateTitle, "Title must be unique for a given trainer.");
        }

        return errors.Match(
            onSuccess: () =>
            {
                Title = title;
                return Result.Success();
            },
            onFailure: Result.Failure
        );
    }
}
