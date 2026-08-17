using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate;

/// <summary>
/// The aggregate only accepts value objects, so malformed input cannot reach it:
/// those rules belong to the value objects and are covered by their own tests, and
/// the accumulation of several errors at once is covered by the application-layer
/// factory. What is left to assert here is the aggregate's own behavior.
/// </summary>
public sealed class TrainerTests
{
    // --- Create ---

    /// <summary>
    /// Create, valid data, sets all properties.
    /// </summary>
    [Fact]
    public void Create_ValidData_SetsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var trainer = new TrainerBuilder()
            .WithFirstname("John")
            .WithLastname("Doe")
            .WithContactEmail("john.doe@example.com")
            .WithBio("Experienced software trainer with 10 years of experience.")
            .WithUserId(userId)
            .Build();

        // Assert
        trainer.Name.Firstname.Should().Be("John");
        trainer.Name.Lastname.Should().Be("Doe");
        trainer.ContactEmail.FullAddress.Should().Be("john.doe@example.com");
        trainer.Bio.Should().NotBeNull();
        trainer.UserId.Value.Should().Be(userId);
    }

    /// <summary>
    /// Create, valid data, raises trainer created event.
    /// </summary>
    [Fact]
    public void Create_ValidData_RaisesTrainerCreatedEvent()
    {
        // Act
        var trainer = new TrainerBuilder().Build();

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerCreatedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.Name.Should().Be(trainer.Name);
        domainEvent.ContactEmail.Should().Be(trainer.ContactEmail);
    }

    /// <summary>
    /// Create, valid data, raises no change event.
    /// </summary>
    [Fact]
    public void Create_ValidData_RaisesNoChangeEvent()
    {
        // A trainer coming into existence must not look like one whose name or
        // contact email just changed.

        // Act
        var trainer = new TrainerBuilder().Build();

        // Assert
        trainer.DomainEvents.Should().ContainSingle();
    }

    /// <summary>
    /// Create, without bio, leaves the bio null.
    /// </summary>
    [Fact]
    public void Create_WithoutBio_LeavesTheBioNull()
    {
        // Act
        var trainer = new TrainerBuilder()
            .WithoutBio()
            .Build();

        // Assert
        trainer.Bio.Should().BeNull();
    }

    /// <summary>
    /// Create, with id, sets specified id.
    /// </summary>
    [Fact]
    public void Create_WithId_SetsSpecifiedId()
    {
        // Arrange
        var specificId = Guid.NewGuid();

        // Act
        var trainer = new TrainerBuilder()
            .WithId(specificId)
            .Build();

        // Assert
        trainer.Id.Value.Should().Be(specificId);
    }

    /// <summary>
    /// Create, null name, throws argument null exception.
    /// </summary>
    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Trainer.Create(
            TrainerId.Generate(), UserId.Create(Guid.NewGuid()), null!, ContactEmail(), null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Create, null contact email, throws argument null exception.
    /// </summary>
    [Fact]
    public void Create_NullContactEmail_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Trainer.Create(
            TrainerId.Generate(), UserId.Create(Guid.NewGuid()), TrainerName(), null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- Edit ---

    /// <summary>
    /// Edit, valid data, updates every editable property.
    /// </summary>
    [Fact]
    public void Edit_ValidData_UpdatesEveryEditableProperty()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();

        // Act
        trainer.Edit(
            TrainerName("Jane", "Smith"),
            ContactEmail("jane.smith@example.com"),
            TrainerBio("Freshly written bio."));

        // Assert
        trainer.Name.Firstname.Should().Be("Jane");
        trainer.Name.Lastname.Should().Be("Smith");
        trainer.ContactEmail.FullAddress.Should().Be("jane.smith@example.com");
        trainer.Bio!.Value.Should().Be("Freshly written bio.");
    }

    /// <summary>
    /// Edit, changed name, raises name changed event carrying old and new names.
    /// </summary>
    [Fact]
    public void Edit_ChangedName_RaisesNameChangedEventCarryingOldAndNewNames()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithFirstname("John")
            .WithLastname("Doe")
            .Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(TrainerName("Jane", "Smith"), ContactEmail(), TrainerBio());

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerNameChangedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.OldName.Should().Be(TrainerName("John", "Doe"));
        domainEvent.NewName.Should().Be(TrainerName("Jane", "Smith"));
    }

    /// <summary>
    /// Edit, changed contact email, raises contact email changed event carrying old and new addresses.
    /// </summary>
    [Fact]
    public void Edit_ChangedContactEmail_RaisesContactEmailChangedEventCarryingOldAndNewAddresses()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithContactEmail("old.email@example.com")
            .Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(TrainerName(), ContactEmail("new.email@example.com"), TrainerBio());

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerContactEmailChangedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.OldContactEmail.Should().Be(ContactEmail("old.email@example.com"));
        domainEvent.NewContactEmail.Should().Be(ContactEmail("new.email@example.com"));
    }

    /// <summary>
    /// Edit, changed name only, raises no contact email changed event.
    /// </summary>
    [Fact]
    public void Edit_ChangedNameOnly_RaisesNoContactEmailChangedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(TrainerName("Jane", "Doe"), ContactEmail(), TrainerBio());

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerNameChangedDomainEvent);
        trainer.DomainEvents.Should().NotContain(e => e is TrainerContactEmailChangedDomainEvent);
    }

    /// <summary>
    /// Edit, unchanged values, raises no event.
    /// </summary>
    [Fact]
    public void Edit_UnchangedValues_RaisesNoEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(TrainerName(), ContactEmail(), TrainerBio());

        // Assert
        trainer.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Edit, null bio, clears the existing bio.
    /// </summary>
    [Fact]
    public void Edit_NullBio_ClearsTheExistingBio()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithBio("Some bio worth clearing.")
            .Build();

        // Act
        trainer.Edit(TrainerName(), ContactEmail(), bio: null);

        // Assert
        trainer.Bio.Should().BeNull();
    }

    /// <summary>
    /// Edit, provided bio, sets the bio of a trainer without one.
    /// </summary>
    [Fact]
    public void Edit_ProvidedBio_SetsTheBioOfATrainerWithoutOne()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithoutBio()
            .Build();

        // Act
        trainer.Edit(TrainerName(), ContactEmail(), TrainerBio("A bio at last."));

        // Assert
        trainer.Bio!.Value.Should().Be("A bio at last.");
    }

    /// <summary>
    /// Edit, null name, throws argument null exception.
    /// </summary>
    [Fact]
    public void Edit_NullName_ThrowsArgumentNullException()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();

        // Act
        var act = () => trainer.Edit(null!, ContactEmail(), null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Edit, null contact email, throws argument null exception.
    /// </summary>
    [Fact]
    public void Edit_NullContactEmail_ThrowsArgumentNullException()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();

        // Act
        var act = () => trainer.Edit(TrainerName(), null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- MarkForDeletion ---

    /// <summary>
    /// Mark for deletion, raises trainer deleted event.
    /// </summary>
    [Fact]
    public void MarkForDeletion_RaisesTrainerDeletedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.MarkForDeletion();

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerDeletedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.PhotoId.Should().BeNull("a trainer without a portrait leaves no bytes to collect");
    }

    /// <summary>
    /// Mark for deletion, with a portrait, carries its identity on the event.
    /// </summary>
    /// <remarks>
    /// The policy that collects the bytes runs after every row that could have answered the
    /// question is gone, so the event carries what the aggregate still knows (ADR 0085) — the
    /// old-address warning's shape, one attribute over.
    /// </remarks>
    [Fact]
    public void MarkForDeletion_WithAPortrait_CarriesItsIdentityOnTheEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        var photo = TrainerPortrait();
        trainer.AttachPhoto(photo);
        trainer.ClearDomainEvents();

        // Act
        trainer.MarkForDeletion();

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerDeletedDomainEvent>().Single();
        domainEvent.PhotoId.Should().Be(photo.PhotoId);
    }

    // --- AttachPhoto and RemovePhoto ---

    /// <summary>
    /// Attach photo, no photo yet, publishes it and displaces nothing.
    /// </summary>
    [Fact]
    public void AttachPhoto_NoPhotoYet_PublishesItAndDisplacesNothing()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        var photo = TrainerPortrait();

        // Act
        trainer.AttachPhoto(photo);

        // Assert
        trainer.Photo.Should().Be(photo);
    }

    /// <summary>
    /// Attach photo, photo already published, replaces it.
    /// </summary>
    /// <remarks>
    /// The caller reads <c>Photo</c> before calling this when it needs to know what it is about to
    /// displace — the old bytes still have to be deleted, after the new profile is committed. The
    /// aggregate does not hand it back: it answers whether a change was allowed, and is not a way
    /// of reading through to the data.
    /// </remarks>
    [Fact]
    public void AttachPhoto_PhotoAlreadyPublished_ReplacesIt()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        var first = TrainerPortrait();
        var second = TrainerPortrait();
        trainer.AttachPhoto(first);

        // Act
        trainer.AttachPhoto(second);

        // Assert
        trainer.Photo.Should().Be(second);
    }

    /// <summary>
    /// Attach photo, null photo, throws.
    /// </summary>
    [Fact]
    public void AttachPhoto_NullPhoto_Throws()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();

        // Act
        var act = () => trainer.AttachPhoto(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Remove photo, photo published, clears it.
    /// </summary>
    [Fact]
    public void RemovePhoto_PhotoPublished_ClearsIt()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        trainer.AttachPhoto(TrainerPortrait());

        // Act
        trainer.RemovePhoto();

        // Assert
        trainer.Photo.Should().BeNull();
    }

    /// <summary>
    /// Remove photo, no photo, changes nothing rather than complaining.
    /// </summary>
    [Fact]
    public void RemovePhoto_NoPhoto_ChangesNothing()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();

        // Act
        trainer.RemovePhoto();

        // Assert
        trainer.Photo.Should().BeNull();
    }

    /// <summary>
    /// Attach photo, raises no domain event.
    /// </summary>
    /// <remarks>
    /// Deliberate. A domain event here would have to be handled, and handlers run inside the
    /// transaction the aggregate is saved in — so deleting the displaced bytes would happen at the
    /// one moment a rollback could still want them back.
    /// </remarks>
    [Fact]
    public void AttachPhoto_RaisesNoDomainEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().Build();
        trainer.ClearDomainEvents();

        // Act
        trainer.AttachPhoto(TrainerPortrait());

        // Assert
        trainer.DomainEvents.Should().BeEmpty();
    }

    private static TrainerPhoto TrainerPortrait() =>
        TrainerPhoto.Create(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00],
            TrainerPhoto.PngContentType,
            DateTime.UtcNow).ShouldBeSuccess();

    private static Name TrainerName(string firstname = "John", string lastname = "Doe")
        => Name.Create(firstname, lastname).ShouldBeSuccess();

    private static Email ContactEmail(string address = "john.doe@example.com")
        => Email.Create(address).ShouldBeSuccess();

    private static Bio TrainerBio(string value = "Experienced software trainer with 10 years of experience.")
        => Bio.Create(value).ShouldBeSuccess();
}
