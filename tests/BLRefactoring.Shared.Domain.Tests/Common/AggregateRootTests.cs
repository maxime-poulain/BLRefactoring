using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class AggregateRootTests
{
    public class TestAggregateId : EntityId<TestAggregateId>
    {
        private TestAggregateId(Guid value) : base(value) { }
    }

    public class TestAggregate() : AggregateRoot<TestAggregateId>(TestAggregateId.Generate())
    {
        // AddDomainEvent/AddDomainEvents are protected: only the aggregate's own
        // behavior methods may raise events. These test hooks play that role here.
        public void RaiseEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
        public void RaiseEvents(IEnumerable<IDomainEvent> domainEvents) => AddDomainEvents(domainEvents);
    }

    public class TestDomainEvent : IDomainEvent { }

    [Fact]
    public void NewAggregateRoot_HasEmptyDomainEvents()
    {
        var aggregate = new TestAggregate();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_EventIsAddedToCollection()
    {
        var aggregate = new TestAggregate();
        var domainEvent = new TestDomainEvent();

        aggregate.RaiseEvent(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

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

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestDomainEvent());
        aggregate.RaiseEvent(new TestDomainEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_ReturnsReadOnlyCollection()
    {
        var aggregate = new TestAggregate();

        var domainEvents = aggregate.DomainEvents;

        domainEvents.Should().NotBeNull();
        domainEvents.Should().NotBeAssignableTo<List<IDomainEvent>>();
    }
}
