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
            .WithEmail("john.doe@example.com")
            .WithBio("Experienced software trainer with 10 years of experience.")
            .WithUserId(userId)
            .BuildValid();

        // Assert
        trainer.Name.Should().NotBeNull();
        trainer.Email.Should().NotBeNull();
        trainer.Bio.Should().NotBeNull();
        trainer.UserId.Should().NotBeNull();
        ((Guid)trainer.UserId).Should().Be(userId);
    }

    [Fact]
    public void Create_WithMessage_ValidData_RaisesTrainerCreatedEvent()
    {
        // Act
        var trainer = new TrainerBuilder().BuildValid();

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerCreatedDomainEvent);
        var domainEvent = trainer.DomainEvents.OfType<TrainerCreatedDomainEvent>().Single();
        domainEvent.Trainer.Should().BeSameAs(trainer);
    }

    [Fact]
    public void Create_WithMessage_InvalidEmail_ReturnsFailure()
    {
        // Act
        var result = new TrainerBuilder()
            .WithEmail("invalid-email")
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
        ((Guid)trainer.Id).Should().Be(specificId);
    }

    // --- ChangeName ---

    [Fact]
    public void ChangeName_ValidNames_ReturnsSuccess()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.ChangeName("Jane", "Smith");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void ChangeName_ValidNames_UpdatesNameProperty()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        var originalName = trainer.Name;

        // Act
        trainer.ChangeName("Jane", "Smith");

        // Assert
        trainer.Name.Should().NotBe(originalName);
    }

    [Fact]
    public void ChangeName_ValidNames_RaisesTrainerNameChangedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.ChangeName("Jane", "Smith");

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerNameChangedDomainEvent);
    }

    [Fact]
    public void ChangeName_InvalidFirstname_ReturnsFailure()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.ChangeName("", "Smith");

        // Assert
        result.ShouldBeFailure();
    }

    // --- ChangeEmail ---

    [Fact]
    public void ChangeEmail_ValidEmail_ReturnsSuccess()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.ChangeEmail("new.email@example.com");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void ChangeEmail_ValidEmail_RaisesTrainerEmailChangedEvent()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();
        trainer.ClearDomainEvents();

        // Act
        trainer.ChangeEmail("new.email@example.com");

        // Assert
        trainer.DomainEvents.Should().ContainSingle(e => e is TrainerEmailChangedDomainEvent);
    }

    [Fact]
    public void ChangeEmail_InvalidEmail_ReturnsFailure()
    {
        // Arrange
        var trainer = new TrainerBuilder().BuildValid();

        // Act
        var result = trainer.ChangeEmail("not-a-valid-email");

        // Assert
        result.ShouldBeFailure();
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
        domainEvent.Trainer.Should().BeSameAs(trainer);
    }
}
