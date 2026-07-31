using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Infrastructure.Json;

/// <summary>
/// Serialises every <see cref="EntityId{TEntityId,TValue}"/> as its bare value, so an outbox
/// payload reads <c>"trainerId": "d4b6b8d2-…"</c> rather than <c>"trainerId": {"value": "…"}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Without this, System.Text.Json cannot handle these identifiers at all: derived id types
/// declare a private constructor and expose <c>Value</c> as get-only, so there is nothing for
/// the serialiser to call on the way in and nothing to assign on the way out. A converter is
/// not an optimisation here, it is the enabling piece.
/// </para>
/// <para>
/// Reading goes through <c>Create</c>, the same single construction path the rest of the code
/// uses, so an identifier restored from a message obeys the invariant an identifier built in
/// memory obeys — an empty value is rejected on the way back in, not silently materialised.
/// </para>
/// <para>
/// One factory covers the whole hierarchy: the base is generic over both the derived type and
/// the underlying value, so matching it means walking up the base chain rather than testing a
/// single base class. New identifier types are picked up with no registration.
/// </para>
/// </remarks>
public sealed class EntityIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => FindEntityIdBase(typeToConvert) is not null;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var entityIdBase = FindEntityIdBase(typeToConvert)
            ?? throw new InvalidOperationException($"{typeToConvert.Name} is not an entity identifier.");

        // EntityId<TEntityId, TValue>: the first argument is the concrete id type, the second
        // the primitive it wraps.
        var arguments = entityIdBase.GetGenericArguments();

        return (JsonConverter)Activator.CreateInstance(
            typeof(EntityIdJsonConverter<,>).MakeGenericType(arguments[0], arguments[1]))!;
    }

    /// <summary>
    /// Finds the <c>EntityId&lt;,&gt;</c> in the base chain of <paramref name="type"/>, if any.
    /// </summary>
    /// <remarks>
    /// <c>TrainerId</c> derives from <c>EntityId&lt;TrainerId&gt;</c>, itself derived from
    /// <c>EntityId&lt;TrainerId, Guid&gt;</c>, so the two-argument base is reached by walking up
    /// rather than by looking at the immediate base type.
    /// </remarks>
    private static Type? FindEntityIdBase(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityId<,>))
            {
                return current;
            }
        }

        return null;
    }
}

/// <summary>
/// Reads and writes a single identifier type as its underlying value.
/// </summary>
internal sealed class EntityIdJsonConverter<TEntityId, TValue> : JsonConverter<TEntityId>
    where TEntityId : EntityId<TEntityId, TValue>
    where TValue : struct, IComparable<TValue>, IEquatable<TValue>
{
    public override TEntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options);

        // Create validates and builds the derived type, so a tampered or empty value fails here
        // rather than producing an identifier the domain would consider impossible.
        return EntityId<TEntityId, TValue>.Create(value);
    }

    public override void Write(Utf8JsonWriter writer, TEntityId value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }

    /// <summary>
    /// Lets an identifier serve as a dictionary key, written as its bare value like everywhere else.
    /// </summary>
    public override void WriteAsPropertyName(
        Utf8JsonWriter writer, TEntityId value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.Value.ToString() ?? string.Empty);
    }
}
