using MediatR;

namespace BLRefactoring.Shared.Common;

public interface IDomainEvent : INotification
{
}

public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent> where TDomainEvent : IDomainEvent
{
}
