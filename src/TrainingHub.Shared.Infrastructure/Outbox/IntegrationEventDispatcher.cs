using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// Routes a delivered integration event to every <see cref="IIntegrationEventHandler{TEvent}"/>
/// registered for it.
/// </summary>
/// <remarks>
/// A switch over the registered event types, fed by constructor injection, on purpose twice over.
/// The switch: reflection over <c>MakeGenericType</c> — or reusing the messaging library's
/// <c>Publish</c> — would route anything, and "routes anything" is how an in-process bus and a
/// post-commit outbox end up indistinguishable again (ADR 0024's rejected alternative). The
/// injection: the container already knows every consumer of every fact, so the dispatcher asks for
/// them in its constructor like any other dependency instead of going shopping with a service
/// locator. The set of integration events is closed and explicit in
/// <see cref="IntegrationEventTypes"/>; a dispatcher that lists the same four lines is not
/// duplication, it is the same decision stated where the routing happens — and a unit test holds
/// the two lists together, exactly as one holds the serializer to the registry.
/// </remarks>
public sealed class IntegrationEventDispatcher(
    IEnumerable<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>> trainerCreatedConsumers,
    IEnumerable<IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>> contactEmailChangedConsumers,
    IEnumerable<IIntegrationEventHandler<TrainingCreatedIntegrationEvent>> trainingCreatedConsumers,
    IEnumerable<IIntegrationEventHandler<TrainingEditedIntegrationEvent>> trainingEditedConsumers)
{
    /// <summary>
    /// Hands the fact to each of its registered consumers, in registration order, skipping the
    /// ones already delivered and isolating each from its neighbours' failures. Answers who
    /// delivered this pass and who failed (ADR 0034).
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="alreadyDelivered">The ledger's answer: consumers this message already reached.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public Task<DispatchOutcome> DispatchAsync(
        IIntegrationEvent integrationEvent,
        IReadOnlySet<string> alreadyDelivered,
        CancellationToken cancellationToken) =>
        integrationEvent switch
        {
            TrainerCreatedIntegrationEvent fact => HandleAllAsync(trainerCreatedConsumers, fact, alreadyDelivered, cancellationToken),
            TrainerContactEmailChangedIntegrationEvent fact => HandleAllAsync(contactEmailChangedConsumers, fact, alreadyDelivered, cancellationToken),
            TrainingCreatedIntegrationEvent fact => HandleAllAsync(trainingCreatedConsumers, fact, alreadyDelivered, cancellationToken),
            TrainingEditedIntegrationEvent fact => HandleAllAsync(trainingEditedConsumers, fact, alreadyDelivered, cancellationToken),
            _ => throw new InvalidOperationException(
                $"{integrationEvent.GetType().Name} has no route in {nameof(IntegrationEventDispatcher)}. " +
                "A new integration event is registered, serialized, consumed — and routed here."),
        };

    private static async Task<DispatchOutcome> HandleAllAsync<TEvent>(
        IEnumerable<IIntegrationEventHandler<TEvent>> consumers,
        TEvent integrationEvent,
        IReadOnlySet<string> alreadyDelivered,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var delivered = new List<string>();
        var failures = new List<ConsumerFailure>();

        foreach (var consumer in consumers)
        {
            if (alreadyDelivered.Contains(consumer.ConsumerName))
            {
                continue;
            }

            try
            {
                await consumer.HandleAsync(integrationEvent, cancellationToken);
                delivered.Add(consumer.ConsumerName);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The isolation itself: a consumer's failure is its own outcome, and its
                // neighbours still get the fact. Cancellation stays a shutdown, not an outcome —
                // it propagates whole, and the pass it aborts re-runs, which is the at-least-once
                // window the lease already implies.
                failures.Add(new ConsumerFailure(consumer.ConsumerName, exception));
            }
        }

        return new DispatchOutcome(delivered, failures);
    }
}
