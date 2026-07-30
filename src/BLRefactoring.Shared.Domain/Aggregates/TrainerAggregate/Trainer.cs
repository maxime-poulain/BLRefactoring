using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

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
    /// Gets the bio of the trainer, or <see langword="null"/> when the trainer
    /// has not provided one yet.
    /// </summary>
    public Bio? Bio { get; private set; }

    /// <summary>
    /// Gets the user ID associated with the trainer.
    /// </summary>
    public UserId UserId { get; private set; } = null!;

    /// <summary>
    /// Private constructor used by the factories and by EF Core constructor
    /// binding (the parameter name matches the <see cref="Entity{TEntityId}.Id"/> property).
    /// </summary>
    private Trainer(TrainerId id) : base(id)
    {
    }

    /// <summary>
    /// Creates a new instance of the Trainer class.
    /// </summary>
    /// <param name="firstname">The first name of the trainer.</param>
    /// <param name="lastname">The last name of the trainer.</param>
    /// <param name="email">The email of the trainer.</param>
    /// <param name="bio">The bio of the trainer.</param>
    /// <param name="userId">The user ID associated with the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create(TrainerCreationMessage trainerCreationMessage)
    {
        var trainer = new Trainer(trainerCreationMessage.TrainerId)
        {
            UserId = trainerCreationMessage.UserId
        };
        return CreateInternal(
            trainerCreationMessage.Firstname,
            trainerCreationMessage.Lastname,
            trainerCreationMessage.Email,
            trainerCreationMessage.Bio,
            trainer);
    }

    /// <summary>
    /// Creates a new instance of the Trainer class.
    /// </summary>
    /// <param name="id">The id of the trainer.</param>
    /// <param name="firstname">The first name of the trainer.</param>
    /// <param name="lastname">The last name of the trainer.</param>
    /// <param name="email">The email of the trainer.</param>
    /// <param name="bio">The bio of the trainer.</param>
    /// <param name="userId">The user ID associated with the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create(Guid id,
        string firstname,
        string lastname,
        string email,
        string? bio,
        Guid userId)
    {
        var trainer = new Trainer(TrainerId.Create(id)) { UserId = UserId.Create(userId) };
        return CreateInternal(firstname, lastname, email, bio, trainer);
    }

    private static Result<Trainer> CreateInternal(
        string firstname,
        string lastname,
        string email,
        string? bio,
        Trainer trainer)
    {
        var errors = new ErrorCollection();

        var nameResult = Name.Create(firstname, lastname)
            .TapError(errs => errors.AddErrors(errs));

        var emailResult = Email.Create(email)
            .TapError(errs => errors.AddErrors(errs));

        // The bio is optional at creation time: null means "no bio yet" and is
        // not an error, while a provided value (even empty) goes through the
        // Bio value object and keeps its validation rules.
        var bioResult = bio is null
            ? null
            : Bio.Create(bio).TapError(errs => errors.AddErrors(errs));

        if (errors.Any())
            return Result<Trainer>.Failure(errors);

        nameResult.Tap(name => trainer.Name = name);
        emailResult.Tap(e => trainer.Email = e);
        bioResult?.Tap(b => trainer.Bio = b);

        trainer.AddDomainEvent(new TrainerCreatedDomainEvent(
            trainer.Id,
            trainer.Name.Firstname,
            trainer.Name.Lastname,
            trainer.Email.FullAddress));
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

        return result.Match(trainerEmail =>
        {
            var oldEmail = Email.FullAddress;
            Email = trainerEmail;
            AddDomainEvent(new TrainerEmailChangedDomainEvent(Id, oldEmail, Email.FullAddress));
            return Result.Success();
        }, Result.Failure);
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

        return result.Match(name =>
        {
            var oldName = Name;
            Name = name;
            AddDomainEvent(new TrainerNameChangedDomainEvent(
                Id, oldName.Firstname, oldName.Lastname, Name.Firstname, Name.Lastname));
            return Result.Success();
        }, Result.Failure);
    }

    /// <summary>
    /// Marks the trainer for deletion.
    /// </summary>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(Id));
    }
}
