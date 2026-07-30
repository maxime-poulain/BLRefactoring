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
    /// Gets the address at which the trainer wishes to be contacted.
    /// </summary>
    /// <remarks>
    /// This is a business attribute of the trainer, not the credential of their
    /// account: authentication is handled by the Identity context, which the
    /// aggregate only ever references through <see cref="UserId"/>. A trainer may
    /// legitimately publish a professional address that differs from the one their
    /// account was opened with, and two trainers of the same organisation may even
    /// share one — hence no uniqueness rule applies here, unlike the account email.
    /// </remarks>
    public Email ContactEmail { get; private set; } = null!;

    /// <summary>
    /// Gets the name of the trainer.
    /// </summary>
    public Name Name { get; private set; } = null!;

    /// <summary>
    /// Gets the bio of the trainer, or <see langword="null"/> when the trainer
    /// has not provided one yet.
    /// </summary>
    public Bio? Bio { get; private set; }

    /// <summary>
    /// Gets the identifier of the identity account the trainer is attached to.
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
    /// <param name="trainerCreationMessage">The data the trainer is created from.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create(TrainerCreationMessage trainerCreationMessage)
    {
        ArgumentNullException.ThrowIfNull(trainerCreationMessage);

        var trainer = new Trainer(trainerCreationMessage.TrainerId)
        {
            UserId = trainerCreationMessage.UserId
        };

        return trainer.ApplyEdition(trainerCreationMessage).Match(
            () =>
            {
                trainer.AddDomainEvent(new TrainerCreatedDomainEvent(
                    trainer.Id,
                    trainer.Name.Firstname,
                    trainer.Name.Lastname,
                    trainer.ContactEmail.FullAddress));
                return Result<Trainer>.Success(trainer);
            },
            Result<Trainer>.Failure);
    }

    /// <summary>
    /// Creates a new instance of the Trainer class.
    /// </summary>
    /// <param name="id">The id of the trainer.</param>
    /// <param name="firstname">The first name of the trainer.</param>
    /// <param name="lastname">The last name of the trainer.</param>
    /// <param name="contactEmail">The contact email of the trainer.</param>
    /// <param name="bio">The bio of the trainer.</param>
    /// <param name="userId">The user ID associated with the trainer.</param>
    /// <returns>A <see cref="Result{T}"/> of type <see cref="Trainer"/>.</returns>
    public static Result<Trainer> Create(Guid id,
        string firstname,
        string lastname,
        string contactEmail,
        string? bio,
        Guid userId)
        => Create(new TrainerCreationMessage
        {
            TrainerId = TrainerId.Create(id),
            UserId = UserId.Create(userId),
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        });

    /// <summary>
    /// Applies the edition described by <paramref name="message"/> to the trainer's
    /// profile and raises one domain event per attribute that actually changed.
    /// </summary>
    /// <remarks>
    /// The profile is edited as a whole — a single form carrying every field — so the
    /// aggregate exposes a single entry point rather than one mutator per attribute:
    /// fine-grained mutators would have no caller, and mixing them with this method
    /// would offer two mutation paths with incompatible semantics (mutate-on-the-spot
    /// versus validate-everything-then-mutate).
    /// </remarks>
    /// <param name="message">The new state of the profile.</param>
    /// <returns>A <see cref="Result"/> indicating whether the operation was successful or not.</returns>
    public Result Edit(TrainerEditionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var previousName = Name;
        var previousContactEmail = ContactEmail;

        return ApplyEdition(message).Tap(() =>
        {
            if (Name != previousName)
            {
                AddDomainEvent(new TrainerNameChangedDomainEvent(
                    Id,
                    previousName.Firstname,
                    previousName.Lastname,
                    Name.Firstname,
                    Name.Lastname));
            }

            if (ContactEmail != previousContactEmail)
            {
                AddDomainEvent(new TrainerContactEmailChangedDomainEvent(
                    Id,
                    previousContactEmail.FullAddress,
                    ContactEmail.FullAddress));
            }
        });
    }

    /// <summary>
    /// Validates the whole <paramref name="message"/> and, only when every input is
    /// valid, mutates the aggregate. Raises no domain event: the callers each raise
    /// the events matching their intent (created vs edited).
    /// </summary>
    private Result ApplyEdition(TrainerEditionMessage message)
    {
        var errors = new ErrorCollection();

        var nameResult = Name.Create(message.Firstname, message.Lastname)
            .TapError(errs => errors.AddErrors(errs));

        var contactEmailResult = Email.Create(message.ContactEmail)
            .TapError(errs => errors.AddErrors(errs));

        // The bio is optional: null means "no bio", which is not an error — at
        // creation time it means none was provided, on edition it clears the
        // current one. A provided value (even empty) goes through the Bio value
        // object and keeps its validation rules.
        var bioResult = message.Bio is null
            ? null
            : Bio.Create(message.Bio).TapError(errs => errors.AddErrors(errs));

        if (errors.Any())
        {
            return Result.Failure(errors);
        }

        nameResult.Tap(name => Name = name);
        contactEmailResult.Tap(email => ContactEmail = email);

        Bio = null;
        bioResult?.Tap(bio => Bio = bio);

        return Result.Success();
    }

    /// <summary>
    /// Marks the trainer for deletion.
    /// </summary>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(Id));
    }
}
