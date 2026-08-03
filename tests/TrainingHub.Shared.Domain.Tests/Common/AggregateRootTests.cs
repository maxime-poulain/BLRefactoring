using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common;

/// <summary>
/// Behaviour covered for <c>AggregateRoot</c>.
/// </summary>
public sealed class AggregateRootTests
{
    /// <summary>
    /// Test aggregate id.
    /// </summary>
    public sealed class TestAggregateId : EntityId<TestAggregateId>
    {
        [SuppressMessage("Style", "IDE0051:Remove unused private members",
            Justification = "EntityId<T>.BuildFactory resolves this constructor with GetConstructor(..., NonPublic) and compiles it into the factory every Create and Generate call goes through. It is the only way an identifier is ever built; the analyzer cannot see a call that a compiled expression tree makes.")]
        private TestAggregateId(Guid value) : base(value) { }
    }

    /// <summary>
    /// Test aggregate.
    /// </summary>
    public sealed class TestAggregate() : AggregateRoot<TestAggregateId>(TestAggregateId.Generate())
    {
        // AddDomainEvent/AddDomainEvents are protected: only the aggregate's own
        // behavior methods may raise events. These test hooks play that role here.

        /// <summary>
        /// Raise event.
        /// </summary>
        public void RaiseEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

        /// <summary>
        /// Raise events.
        /// </summary>
        public void RaiseEvents(IEnumerable<IDomainEvent> domainEvents) => AddDomainEvents(domainEvents);
    }

    /// <summary>
    /// Test domain event.
    /// </summary>
    public sealed class TestDomainEvent : IDomainEvent { }

    /// <summary>
    /// New aggregate root, has empty domain events.
    /// </summary>
    [Fact]
    public void NewAggregateRoot_HasEmptyDomainEvents()
    {
        var aggregate = new TestAggregate();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Add domain event, event is added to collection.
    /// </summary>
    [Fact]
    public void AddDomainEvent_EventIsAddedToCollection()
    {
        var aggregate = new TestAggregate();
        var domainEvent = new TestDomainEvent();

        aggregate.RaiseEvent(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    /// <summary>
    /// Add domain events, multiple events are added.
    /// </summary>
    [Fact]
    public void AddDomainEvents_MultipleEventsAreAdded()
    {
        var aggregate = new TestAggregate();
        var event1 = new TestDomainEvent();
        var event2 = new TestDomainEvent();

        aggregate.RaiseEvents([event1, event2]);

        aggregate.DomainEvents.Should().HaveCount(2);
        aggregate.DomainEvents.Should().Contain(event1);
        aggregate.DomainEvents.Should().Contain(event2);
    }

    /// <summary>
    /// Clear domain events, removes all events.
    /// </summary>
    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestDomainEvent());
        aggregate.RaiseEvent(new TestDomainEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Domain events, returns read only collection.
    /// </summary>
    [Fact]
    public void DomainEvents_ReturnsReadOnlyCollection()
    {
        var aggregate = new TestAggregate();

        var domainEvents = aggregate.DomainEvents;

        domainEvents.Should().NotBeNull();
        domainEvents.Should().NotBeAssignableTo<List<IDomainEvent>>();
    }
}
