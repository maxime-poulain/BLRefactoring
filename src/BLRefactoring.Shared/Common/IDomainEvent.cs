using Mediator;

namespace BLRefactoring.Shared.Common;

/// <summary>
/// <para>
/// IDomainEvent represents a state change within an Aggregate in a specific bounded context.
/// Domain Events encapsulate outcomes of domain operations and are essential elements of
/// Domain-Driven Design (DDD). They provide a mechanism to capture and communicate
/// important changes within a specific bounded context.
///</para>
///
/// <para>
/// In a microservice architecture, where several bounded contexts exist, each bounded context
/// handles a specific domain area. For example, in an e-commerce system, an 'Order' bounded
/// context could handle all functionality related to orders.
/// In such a scenario, an Order entity might raise an 'OrderReceived' Domain Event to signify
/// a new order. Components within the 'Order' bounded context can then react to the
/// 'OrderReceived' event as needed.
/// </para>
///
/// <para>
/// However, for communicating state changes across different bounded contexts, Integration Events
/// are utilized. These are special kinds of events that convey information meaningful to
/// multiple bounded contexts. For instance, a 'PaymentProcessed' Integration Event from a 'Payment'
/// bounded context could trigger actions in an 'Order' bounded context.
/// </para>
/// <para>
/// By employing Domain Events and Integration Events, microservice architectures achieve improved
/// communication, modularity, and extensibility.It's crucial for developers to understand these
/// DDD concepts to design robust and maintainable systems.
/// </para>
/// </summary>
public interface IDomainEvent : INotification
{
}

/// <summary>
/// Represents a handler for a specific type of <see cref="IDomainEvent"/> within
/// a bounded context.
///
/// <para>
/// Each handler is responsible for executing specific logic when a Domain Event of a
/// corresponding type is raised. For example, an 'OrderReceivedHandler' could be an
/// IDomainEventHandler for 'OrderReceived' Domain Events, responsible for tasks such as
/// updating the order status or logging the event.
/// </para>
///
/// <para>
/// In a microservice architecture, implementing Domain Event handlers provides a modular
/// way to manage actions in response to state changes. It ensures that logic is separated
/// and only triggered when necessary, aiding in maintainability and extensibility.
/// </para>
///
/// <para>
/// Usually <see cref="IDomainEventHandler{TDomainEvent}"/> implementations reside in the
/// application layer.
/// </para>
/// </summary>
public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
where TDomainEvent : IDomainEvent
{
}
