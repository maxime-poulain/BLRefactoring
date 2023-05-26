using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

public sealed class Trainer : AggregateRoot<TrainerId>
{
    public Email Email { get; private set; } = null!;
    public Name Name { get; private set; } = null!;

    private Trainer() { } // Private constructor for ORM or serialization

    public static Result<Trainer> Create(string firstname, string lastname, string email)
    {
        var trainer = new Trainer();
        return CreateInternal(firstname, lastname, email, trainer);
    }

    public static Result<Trainer> Create(Guid id, string firstname, string lastname, string email)
    {
        var trainer = new Trainer() { Id = (TrainerId)id };
        return CreateInternal(firstname, lastname, email, trainer);
    }

    private static Result<Trainer> CreateInternal(
        string firstname,
        string lastname,
        string email,
        Trainer trainer)
    {
        var result = Result.Success()
            .Combine(() => trainer.ChangeName(firstname, lastname))
            .Combine(() => trainer.ChangeEmail(email));

        if (result.IsFailure)
        {
            return Result<Trainer>.Failure(result.Errors);
        }

        return Result<Trainer>.Success(trainer);
    }

    public Result ChangeEmail(string email)
    {
        var result = Email.Create(email);

        if (result.IsFailure)
        {
            return Result.Failure(result.Errors);
        }

        Email = result.Value;
        return Result.Success();
    }

    public Result ChangeName(string firstname, string lastname)
    {
        var result = Name.Create(firstname, lastname);

        if (result.IsFailure)
        {
            return Result.Failure(result.Errors);
        }

        Name = result.Value;
        return Result.Success();
    }

    // ------------------------------
    // Alternative implementation
    // ------------------------------

    public Result ChangeName2(Name name)
    {
        Name = name;
        return Result.Success();
    }

    public Result ChangeEmail2(Email email)
    {
        Email = email;
        return Result.Success();
    }

    public static Result<Trainer> Create2(Name name, Email email)
    {
        var trainer = new Trainer();
        var result = trainer.ChangeName2(name).CombineWith(trainer, trainer => trainer.ChangeEmail2(email));
        if (result.IsFailure)
        {
            return Result<Trainer>.Failure(result.Errors);
        }
        return Result<Trainer>.Success(trainer);
    }

    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(this));
    }
}
