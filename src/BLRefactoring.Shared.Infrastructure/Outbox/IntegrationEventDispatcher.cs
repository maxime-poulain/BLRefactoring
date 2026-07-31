using System.Collections.Concurrent;
using System.Reflection;
using BLRefactoring.Shared.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// Resolves the handlers registered for an integration event's runtime type and invokes them.
/// </summary>
/// <remarks>
/// A message comes back from the table as an <see cref="IIntegrationEvent"/> whose concrete type
/// is only known at run time, while handlers are registered against the closed generic
/// <c>IIntegrationEventHandler&lt;TEvent&gt;</c>. Bridging the two needs reflection once per
/// event type; the resulting method is cached, so the cost is paid on the first message of each
/// kind rather than on every message.
/// </remarks>
public sealed class IntegrationEventDispatcher(IServiceProvider serviceProvider) : IIntegrationEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> Cache = new();

    public async Task DispatchAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var (handlerType, handle) = Cache.GetOrAdd(integrationEvent.GetType(), eventType =>
        {
            var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var method = handlerInterface.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;
            return (handlerInterface, method);
        });

        foreach (var handler in serviceProvider.GetServices(handlerType))
        {
            // Awaited one at a time and not swallowed: a throwing handler aborts the message,
            // which stays pending and is retried. Publishing part of a message's effects and
            // reporting success would lose the rest for good.
            await (Task)handle.Invoke(handler, [integrationEvent, cancellationToken])!;
        }
    }
}
