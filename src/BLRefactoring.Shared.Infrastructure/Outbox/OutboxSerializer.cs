using System.Text.Json;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Infrastructure.Json;

namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// The single serialisation contract of the outbox, shared by the writer and the processor.
/// </summary>
/// <remarks>
/// Kept in one place on purpose: a message is written by one side and read by the other, quite
/// possibly by a different deployment, so the two must never drift on naming policy or
/// converters. Web defaults give camelCase, which is what the rest of the application already
/// emits over HTTP.
/// </remarks>
public static class OutboxSerializer
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new EntityIdJsonConverterFactory());
        return options;
    }

    public static string Serialize(IIntegrationEvent integrationEvent)
        => JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);

    /// <summary>
    /// Rebuilds an integration event from its stored payload and type name.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type no longer exists — a message written by a version of the code that
    /// has since removed the contract. Failing loudly beats skipping it silently.
    /// </exception>
    public static IIntegrationEvent Deserialize(string typeName, string payload)
    {
        var type = System.Type.GetType(typeName)
            ?? throw new InvalidOperationException(
                $"Integration event type '{typeName}' could not be resolved. The contract was " +
                "removed or renamed while a message referencing it was still pending.");

        return (IIntegrationEvent)JsonSerializer.Deserialize(payload, type, Options)!;
    }
}
