using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate;

public class TrainerTests
{
    // --- Create via TrainerCreationMessage ---

    [Fact]
    public void Create_WithMessage_ValidData_ReturnsSuccess()
    {
        // Act
        var result = new TrainerBuilder().Build();

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_WithMessage_ValidData_SetsAllProperties()
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
            .BuildValid();

        // Assert
        trainer.Name.Should().NotBeNull();
        trainer.ContactEmail.Should().NotBeNull();
        trainer.Bio.Should().NotBeNull();
        trainer.UserId.Should().NotBeNull();
        trainer.UserId.Value.Should().Be(userId);
    }

    [Fact]
    public void Create_WithMessage_ValidData_RaisesTrainerCreatedEvent()
    {
        // Act
        var trainer = new TrainerBuilder().BuildValid();

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerCreatedDomainEvent);
        var domainEvent = trainer.DomainEvents.OfType<TrainerCreatedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.Firstname.Should().Be(trainer.Name.Firstname);
        domainEvent.Lastname.Should().Be(trainer.Name.Lastname);
        domainEvent.ContactEmail.Should().Be(trainer.ContactEmail.FullAddress);
    }

    [Fact]
    public void Create_WithMessage_ValidData_RaisesNoChangeEvent()
    {
        // Creation shares its validate-then-mutate path with edition; a trainer
        // coming into existence must not look like one whose name or contact
        // email just changed.

        // Act
        var trainer = new TrainerBuilder().BuildValid();

        // Assert
        trainer.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithMessage_InvalidEmail_ReturnsFailure()
    {
        // Act
        var result = new TrainerBuilder()
            .WithContactEmail("invalid-email")
            .Build();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Create_WithMessage_InvalidName_ReturnsFailure()
    {
        // Act
        var result = new TrainerBuilder()
            .WithFirstname("")
            .Build();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Create_WithMessage_InvalidBio_ReturnsFailure()
    {
        // Act
        var result = new TrainerBuilder()
            .WithBio("")
            .Build();

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Create_WithMessage_WithoutBio_ReturnsSuccessWithNullBio()
    {
        // Act
        var trainer = new TrainerBuilder()
            .WithoutBio()
            .BuildValid();

        // Assert
        trainer.Bio.Should().BeNull();
    }

    [Fact]
    public void Create_NullMessage_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Trainer.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- Create via (Guid, ...) ---

    [Fact]
    public void Create_WithId_ValidData_SetsSpecifiedId()
    {
        // Arrange
        var specificId = Guid.NewGuid();

        // Act
        var trainer = new TrainerBuilder()
            .WithId(specificId)
            .BuildValid();

        // Assert
        trainer.Id.Value.Should().Be(specificId);
    }

    // --- Edit ---

    [Fact]
    public void Edit_ValidData_ReturnsSuccess()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(firstname: "Jane", lastname: "Smith"));

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Edit_ValidData_UpdatesEveryEditableProperty()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        trainer.Edit(EditionMessage(
            firstname: "Jane",
            lastname: "Smith",
            contactEmail: "jane.smith@example.com",
            bio: "Freshly written bio."));

        // Assert
        trainer.Name.Firstname.Should().Be("Jane");
        trainer.Name.Lastname.Should().Be("Smith");
        trainer.ContactEmail.FullAddress.Should().Be("jane.smith@example.com");
        trainer.Bio!.Value.Should().Be("Freshly written bio.");
    }

    [Fact]
    public void Edit_ChangedName_RaisesTrainerNameChangedEventCarryingOldAndNewNames()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithFirstname("John")
            .WithLastname("Doe")
            .BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(EditionMessage(firstname: "Jane", lastname: "Smith"));

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerNameChangedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.OldFirstname.Should().Be("John");
        domainEvent.OldLastname.Should().Be("Doe");
        domainEvent.NewFirstname.Should().Be("Jane");
        domainEvent.NewLastname.Should().Be("Smith");
    }

    [Fact]
    public void Edit_ChangedContactEmail_RaisesContactEmailChangedEventCarryingOldAndNewAddresses()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithContactEmail("old.email@example.com")
            .BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(EditionMessage(contactEmail: "new.email@example.com"));

        // Assert
        var domainEvent = trainer.DomainEvents.OfType<TrainerContactEmailChangedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
        domainEvent.OldContactEmail.Should().Be("old.email@example.com");
        domainEvent.NewContactEmail.Should().Be("new.email@example.com");
    }

    [Fact]
    public void Edit_ChangedNameOnly_RaisesNoContactEmailChangedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(EditionMessage(firstname: "Jane"));

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerNameChangedDomainEvent);
        trainer.DomainEvents.Should().NotContain(e => e is TrainerContactEmailChangedDomainEvent);
    }

    [Fact]
    public void Edit_UnchangedValues_RaisesNoEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        trainer.ClearDomainEvents();

        // Act
        var result = trainer.Edit(EditionMessage());

        // Assert
        result.ShouldBeSuccess();
        trainer.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_InvalidFirstname_ReturnsFailure()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(firstname: ""));

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Edit_InvalidContactEmail_ReturnsFailure()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(contactEmail: "not-a-valid-email"));

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Edit_SeveralInvalidFields_AccumulatesEveryError()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(
            firstname: "",
            contactEmail: "not-a-valid-email",
            bio: new string('x', 501)));

        // Assert
        var errors = result.ShouldBeFailure();
        errors.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public void Edit_InvalidData_LeavesAggregateUntouched()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithFirstname("John")
            .WithLastname("Doe")
            .WithContactEmail("john.doe@example.com")
            .BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.Edit(EditionMessage(firstname: "Jane", contactEmail: "not-a-valid-email"));

        // Assert — the valid firstname must not have been applied either
        trainer.Name.Firstname.Should().Be("John");
        trainer.Name.Lastname.Should().Be("Doe");
        trainer.ContactEmail.FullAddress.Should().Be("john.doe@example.com");
        trainer.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_NullBio_ClearsTheExistingBio()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithBio("Some bio worth clearing.")
            .BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(bio: null));

        // Assert
        result.ShouldBeSuccess();
        trainer.Bio.Should().BeNull();
    }

    [Fact]
    public void Edit_ProvidedBio_SetsTheBioOfATrainerWithoutOne()
    {
        // Arrange
        var trainer = new TrainerBuilder()
            .WithoutBio()
            .BuildValid();

        // Act
        trainer.Edit(EditionMessage(bio: "A bio at last."));

        // Assert
        trainer.Bio!.Value.Should().Be("A bio at last.");
    }

    [Fact]
    public void Edit_EmptyBio_ReturnsFailure()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.Edit(EditionMessage(bio: ""));

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Edit_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var act = () => trainer.Edit(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // --- MarkForDeletion ---

    [Fact]
    public void MarkForDeletion_RaisesTrainerDeletedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.MarkForDeletion();

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerDeletedDomainEvent);
        var domainEvent = trainer.DomainEvents.OfType<TrainerDeletedDomainEvent>().Single();
        domainEvent.TrainerId.Should().Be(trainer.Id);
    }

    /// <summary>
    /// Builds an edition message matching the profile <see cref="TrainerBuilder"/>
    /// produces by default, so each test only states what it is actually varying.
    /// </summary>
    private static TrainerEditionMessage EditionMessage(
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer with 10 years of experience.")
        => new()
        {
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };
}
