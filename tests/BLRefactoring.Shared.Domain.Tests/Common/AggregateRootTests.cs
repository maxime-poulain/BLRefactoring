using BLRefactoring.Shared.Common;
using FluentAssertions;
using Mediator;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class AggregateRootTests
{
    public class TestAggregateId : EntityId<TestAggregateId> { }

    public class TestAggregate : AggregateRoot<TestAggregateId> { }

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

        aggregate.AddDomainEvent(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    [Fact]
    public void AddDomainEvents_MultipleEventsAreAdded()
    {
        var aggregate = new TestAggregate();
        var event1 = new TestDomainEvent();
        var event2 = new TestDomainEvent();

        aggregate.AddDomainEvents([event1, event2]);

        aggregate.DomainEvents.Should().HaveCount(2);
        aggregate.DomainEvents.Should().Contain(event1);
        aggregate.DomainEvents.Should().Contain(event2);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.AddDomainEvent(new TestDomainEvent());
        aggregate.AddDomainEvent(new TestDomainEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveDomainEvent_RemovesSpecificEvent()
    {
        var aggregate = new TestAggregate();
        var event1 = new TestDomainEvent();
        var event2 = new TestDomainEvent();
        aggregate.AddDomainEvent(event1);
        aggregate.AddDomainEvent(event2);

        aggregate.RemoveDomainEvent(event1);

        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(event2);
    }

    [Fact]
    public void RemoveDomainEvent_NonExistingEvent_DoesNotThrow()
    {
        var aggregate = new TestAggregate();
        var domainEvent = new TestDomainEvent();

        var act = () => aggregate.RemoveDomainEvent(domainEvent);

        act.Should().NotThrow();
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
