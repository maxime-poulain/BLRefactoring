using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;

public sealed class Training : AggregateRoot<Guid>
{
    public string Title { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public Guid TrainerIdd { get; private set; }
    public List<Rate> Rates { get; private set; }

    private Training() { } // Private constructor for ORM or serialization

    private static Training CreateDraft() => new();

    public static async Task<Result<Training>> CreateAsync(
        string title,
        DateTime startDate,
        DateTime endDate,
        Trainer trainer,
        List<Rate> rates,
        IUniquenessTitleChecker titleChecker)
    {

        var training = CreateDraft();

        var errors = new ErrorCollection();
        errors.AddErrors(await training.ChangeTitleAsync(title, titleChecker, trainer));
        errors.AddErrors(training.ChangeEndDate(endDate));
        errors.AddErrors(training.ChangeStartDate(startDate));
        errors.AddErrors(training.ChangeTrainer(trainer));
        errors.AddErrors(training.ChangeRates(rates));

        // Return the result based on the validation
        if (errors.HasErrors())
        {
            return Result<Training>.Failure(errors);
        }

        return Result<Training>.Success(training);
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
        TrainerIdd = trainer?.Id ?? Guid.NewGuid();
        return Result.Success();
        if (trainer == null || trainer.IsTransient())
        {
            return Result.Failure(ErrorCode.InvalidTrainer, "Trainer cannot be null.");
        }
        TrainerIdd = trainer.Id;
        return Result.Success();
    }

    public Result ChangeRates(List<Rate> rates)
    {
        if (rates == null)
        {
            return Result.Failure(ErrorCode.InvalidRates, "Rates cannot be null.");
        }
        Rates = rates;
        return Result.Success();
    }

    public async Task<Result> ChangeTitleAsync(string title, IUniquenessTitleChecker checker, Trainer trainer)
    {
        var errors = new ErrorCollection();
        if (string.IsNullOrEmpty(title) || title.Length < 5 || title.Length > 30)
        {
            errors.Add(ErrorCode.InvalidTitle, "Title must be between 5 and 30 characters.");
        }

        if (!await checker.IsTitleUniqueAsync(title, trainer))
        {
            errors.Add(ErrorCode.DuplicateTitle, "Title must be unique for a given trainer.");
        }

        if (errors.HasErrors())
        {
            return Result.Failure(errors);
        }

        Title = title;
        return Result.Success();
    }
}
