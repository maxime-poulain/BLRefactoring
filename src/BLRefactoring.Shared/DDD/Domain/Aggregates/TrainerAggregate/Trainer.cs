using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Represents an aggregate root entity for a Trainer.
/// </summary>
public sealed class Trainer : AggregateRoot<TrainerId>
{
    /// <summary>
    /// Gets or sets the email of the trainer.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Gets or sets the name of the trainer.
    /// </summary>
    public Name Name { get; private set; } = null!;

    /// <summary>
    /// Private constructor for ORM or serialization.
    /// </summary>
    private Trainer() { }

    /// <summary>
    /// Creates a new instance of the Trainer class.
    /// </summary>
    /// <param name="firstname">The first name of the trainer.</param>
    /// <param name="lastname">The last name of the trainer.</param>
    /// <param name="email">The email of the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create(string firstname, string lastname, string email)
    {
        var trainer = new Trainer();
        return CreateInternal(firstname, lastname, email, trainer);
    }

    /// <summary>
    /// Creates a new instance of the Trainer class.
    /// </summary>
    /// <param name="id">The id of the trainer.</param>
    /// <param name="firstname">The first name of the trainer.</param>
    /// <param name="lastname">The last name of the trainer.</param>
    /// <param name="email">The email of the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
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

        trainer.AddDomainEvent(new TrainerCreatedDomainEvent(trainer));

        return Result<Trainer>.Success(trainer);
    }

    /// <summary>
    /// Changes the email of the trainer.
    /// </summary>
    /// <param name="email">The new email of the trainer.</param>
    /// <returns><see cref="Result"/> indicating whether the operation was successful or not.</returns>
    public Result ChangeEmail(string email)
    {
        var result = Email.Create(email);

        if (result.IsFailure)
        {
            return Result.Failure(result.Errors);
        }

        Email = result.Value;

        AddDomainEvent(new TrainerEmailChangedDomainEvent(this));

        return Result.Success();
    }

    /// <summary>
    /// Changes the name of the trainer.
    /// </summary>
    /// <param name="firstname">The new first name of the trainer.</param>
    /// <param name="lastname">The new last name of the trainer.</param>
    /// <returns>A <see cref="Result"/> indicating whether the operation was successful or not.</returns>
    public Result ChangeName(string firstname, string lastname)
    {
        var result = Name.Create(firstname, lastname);

        if (result.IsFailure)
        {
            return Result.Failure(result.Errors);
        }

        Name = result.Value;

        AddDomainEvent(new TrainerNameChangedDomainEvent(this));

        return Result.Success();
    }

    // ------------------------------
    // Alternative implementation
    // ------------------------------

    /// <summary>
    /// Changes the name of the trainer with a Name value object.
    /// </summary>
    /// <param name="name">The new name of the trainer.</param>
    /// <returns>A <see cref="Result"/> indicating whether the operation was successful or not.</returns>
    public Result ChangeName2(Name name)
    {
        Name = name;

        AddDomainEvent(new TrainerNameChangedDomainEvent(this));

        return Result.Success();
    }

    /// <summary>
    /// Changes the email of the trainer with an Email value object.
    /// </summary>
    /// <param name="email">The new email of the trainer.</param>
    /// <returns>A <see cref="Result"/> indicating whether the operation was successful or not.</returns>
    public Result ChangeEmail2(Email email)
    {
        Email = email;

        AddDomainEvent(new TrainerEmailChangedDomainEvent(this));

        return Result.Success();
    }

    /// <summary>
    /// Creates a new instance of the Trainer class with Name and Email value objects.
    /// </summary>
    /// <param name="name">The name of the trainer.</param>
    /// <param name="email">The email of the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create2(Name name, Email email)
    {
        var trainer = new Trainer();
        var result = trainer.ChangeName2(name).CombineWith(trainer, trainer => trainer.ChangeEmail2(email));
        if (result.IsFailure)
        {
            return Result<Trainer>.Failure(result.Errors);
        }

        trainer.AddDomainEvent(new TrainerCreatedDomainEvent(trainer));

        return Result<Trainer>.Success(trainer);
    }

    /// <summary>
    /// Marks the trainer for deletion.
    /// </summary>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(this));
    }
}
