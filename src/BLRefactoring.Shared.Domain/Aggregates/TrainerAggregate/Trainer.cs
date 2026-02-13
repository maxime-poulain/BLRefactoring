using BLRefactoring.Shared.Common;
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
    /// Gets the bio of the trainer.
    /// </summary>
    public Bio Bio { get; private set; } = null!;

    /// <summary>
    /// Gets the user ID associated with the trainer.
    /// </summary>
    public UserId UserId { get; private set; } = null!;

    /// <summary>
    /// Private constructor for ORM or serialization.
    /// </summary>
    private Trainer()
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
        var trainer = new Trainer() { UserId = (UserId)trainerCreationMessage.UserId };
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
        string bio,
        Guid userId)
    {
        var trainer = new Trainer() { Id = (TrainerId)id, UserId = (UserId)userId };
        return CreateInternal(firstname, lastname, email, bio, trainer);
    }

    private static Result<Trainer> CreateInternal(
        string firstname,
        string lastname,
        string email,
        string bio,
        Trainer trainer)
    {
        return trainer
            .ChangeName(firstname, lastname)
            .Bind(() => trainer.ChangeEmail(email))
            .Bind(() => Bio.Create(bio).Match(
                createdBio =>
                {
                    trainer.Bio = createdBio;
                    return Result.Success();
                },
                Result.Failure))
            .Match(() =>
            {
                trainer.AddDomainEvent(new TrainerCreatedDomainEvent(trainer));
                return Result<Trainer>.Success(trainer);
            }, Result<Trainer>.Failure);
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
            AddDomainEvent(new TrainerEmailChangedDomainEvent(this));
            Email = trainerEmail;
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

        if (!IsTransient())
        {
            AddDomainEvent(new TrainerNameChangedDomainEvent(this));
        }

        return result.Match(name =>
        {
            Name = name;
            return Result.Success();
        }, Result.Failure);
    }

    /// <summary>
    /// Marks the trainer for deletion.
    /// </summary>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(this));
    }
}
