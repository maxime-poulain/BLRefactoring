using System.Text.Json;
using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// Turns an integration event into the JSON an outbox row stores, and back.
/// </summary>
/// <remarks>
/// Both directions live together on purpose: the write side proves in this very assembly that what
/// it stores can be read back, and the round-trip test holds the pair to it for every registered
/// event. The type to deserialize into is never taken from the payload — it comes from the
/// envelope's name and version through <see cref="IntegrationEventTypes"/>, so a payload cannot
/// smuggle in a type of its own choosing.
/// </remarks>
public static class IntegrationEventSerializer
{
    // One instance, cached: options are expensive to build and immutable in use (CA1869). The
    // defaults are kept deliberately — property names as declared, no indentation — because the
    // payload is an internal wire format, read back through this same class.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default);

    /// <summary>
    /// Serializes <paramref name="integrationEvent"/> into the payload its outbox row stores.
    /// </summary>
    /// <param name="integrationEvent">The event to serialize.</param>
    public static string Serialize(IIntegrationEvent integrationEvent) =>
        // The runtime type, never the declared one: serializing as IIntegrationEvent would write
        // the interface's properties — that is, nothing — and every payload would be "{}".
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);

    /// <summary>
    /// Deserializes a stored payload back into the event its envelope names — the delivery
    /// worker's read path.
    /// </summary>
    /// <param name="name">The stable name on the envelope.</param>
    /// <param name="version">The version on the envelope.</param>
    /// <param name="payload">The stored JSON.</param>
    public static IIntegrationEvent Deserialize(string name, int version, string payload) =>
        JsonSerializer.Deserialize(payload, IntegrationEventTypes.Resolve(name, version), Options)
            as IIntegrationEvent
        ?? throw new InvalidOperationException(
            $"The payload stored for '{name}' v{version} deserialized to null. The row is corrupt " +
            "or was written by something other than this serializer.");
}
