using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
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
    /// Gets the portrait the trainer publishes, or <see langword="null"/> when they have none.
    /// </summary>
    /// <remarks>
    /// The aggregate holds the photo's identity and never its address. Where the bytes are kept is
    /// the infrastructure's business, exactly as it is for every other attribute here.
    /// </remarks>
    public TrainerPhoto? Photo { get; private set; }

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
    /// Creates a trainer from value objects that are, by construction, already valid.
    /// </summary>
    /// <remarks>
    /// Nothing here can fail, so the factory hands back a <see cref="Trainer"/>
    /// rather than a result to unwrap: assembling valid parts cannot produce an
    /// invalid whole as long as the aggregate carries no cross-field rule. Turning
    /// the caller's input into these value objects is the application layer's job —
    /// the domain never sees the shape of the message it came from.
    /// </remarks>
    /// <param name="id">The identifier of the trainer, generated upfront by the caller.</param>
    /// <param name="userId">The identity account the trainer is attached to.</param>
    /// <param name="name">The name of the trainer.</param>
    /// <param name="contactEmail">The address at which the trainer wishes to be contacted.</param>
    /// <param name="bio">The bio of the trainer, or <see langword="null"/> when none was provided.</param>
    public static Trainer Create(TrainerId id, UserId userId, Name name, Email contactEmail, Bio? bio)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contactEmail);

        var trainer = new Trainer(id)
        {
            UserId = userId,
            Name = name,
            ContactEmail = contactEmail,
            Bio = bio
        };

        trainer.AddDomainEvent(new TrainerCreatedDomainEvent(
            trainer.Id, trainer.Name, trainer.ContactEmail));

        return trainer;
    }

    /// <summary>
    /// Replaces the profile of the trainer and raises one domain event per attribute
    /// that actually changed.
    /// </summary>
    /// <remarks>
    /// The profile is edited as a whole — a single form carrying every field — so the
    /// aggregate exposes a single entry point rather than one mutator per attribute.
    /// Every argument is already a valid value object, so the edition cannot fail and
    /// the aggregate can never be left half-edited.
    /// </remarks>
    /// <param name="name">The new name of the trainer.</param>
    /// <param name="contactEmail">The new contact address of the trainer.</param>
    /// <param name="bio">The new bio, or <see langword="null"/> to clear the current one.</param>
    public void Edit(Name name, Email contactEmail, Bio? bio)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contactEmail);

        if (Name != name)
        {
            AddDomainEvent(new TrainerNameChangedDomainEvent(Id, Name, name));
        }

        if (ContactEmail != contactEmail)
        {
            AddDomainEvent(new TrainerContactEmailChangedDomainEvent(Id, ContactEmail, contactEmail));
        }

        Name = name;
        ContactEmail = contactEmail;
        Bio = bio;
    }

    /// <summary>
    /// Attaches a photo to the trainer, replacing any it already had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The photo displaced by this call still has bytes in a store somewhere, and the caller is
    /// the one who has to delete them — after the new profile is committed, which is the ordering
    /// that keeps a row from ever naming an object that is not there. The caller reads
    /// <see cref="Photo"/> beforehand to know what it will have to clean up; this method hands
    /// nothing back, because an aggregate answers whether a change was allowed and does not double
    /// as a way of reading through it.
    /// </para>
    /// <para>
    /// No domain event is raised. One would have to be handled, and handlers here run inside the
    /// transaction the aggregate is being saved in — which is the worst possible place to delete a
    /// blob that a rollback would want back.
    /// </para>
    /// </remarks>
    /// <param name="photo">The photo to publish.</param>
    public void AttachPhoto(TrainerPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        Photo = photo;
    }

    /// <summary>
    /// Removes the trainer's photo, if there is one.
    /// </summary>
    /// <remarks>
    /// Calling this on a trainer without a photo changes nothing and is not an error — whether
    /// there was one to remove is a question the caller answers by reading <see cref="Photo"/>
    /// first, which is also how it learns which bytes it will need to delete afterwards.
    /// </remarks>
    public void RemovePhoto() => Photo = null;

    /// <summary>
    /// Marks the trainer for deletion, announcing the fact so that what depends on the trainer —
    /// their trainings, first of all — can be dealt with in the same unit of work.
    /// </summary>
    /// <remarks>
    /// No use case reaches this today, and that is deliberate rather than an oversight. Removing a
    /// trainer is an administrative decision: a trainer never deletes themselves, and no endpoint
    /// exposes the operation until there is a role entitled to it. What the aggregate states here
    /// is the rule itself — a trainer does not disappear silently, their trainings go with them —
    /// and that rule holds whoever ends up triggering it.
    /// </remarks>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainerDeletedDomainEvent(Id));
    }
}
