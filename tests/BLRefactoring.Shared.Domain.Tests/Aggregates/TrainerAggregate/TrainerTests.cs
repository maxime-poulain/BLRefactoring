using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate;

/// <summary>
/// The aggregate only accepts value objects, so malformed input cannot reach it:
/// those rules belong to the value objects and are covered by their own tests, and
/// the accumulation of several errors at once is covered by the application-layer
/// factory. What is left to assert here is the aggregate's own behaviour.
/// </summary>
public sealed class TrainerTests
{
    // --- Create ---

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

    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Trainer.Create(
            TrainerId.Generate(), UserId.Create(Guid.NewGuid()), null!, ContactEmail(), null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

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
    }

    private static Name TrainerName(string firstname = "John", string lastname = "Doe")
        => Name.Create(firstname, lastname).ShouldBeSuccess();

    private static Email ContactEmail(string address = "john.doe@example.com")
        => Email.Create(address).ShouldBeSuccess();

    private static Bio TrainerBio(string value = "Experienced software trainer with 10 years of experience.")
        => Bio.Create(value).ShouldBeSuccess();
}
