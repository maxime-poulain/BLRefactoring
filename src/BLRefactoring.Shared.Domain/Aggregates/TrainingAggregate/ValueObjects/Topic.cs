using System.Diagnostics.CodeAnalysis;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

public sealed class Topic : ValueObject
{
    public static readonly Topic Programming = new Topic("Programming");
    public static readonly Topic Design = new Topic("Design");
    public static readonly Topic Marketing = new Topic("Marketing");
    public static readonly Topic Business = new Topic("Business");
    public static readonly Topic PersonalDevelopment = new Topic("Personal Development");
    public static readonly Topic Leadership = new Topic("Leadership");

    public string Name { get; init; } = null!;

    private Topic() { } // For ORM

    private Topic(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
    private static List<Topic>? _cachedTopics;
    public static List<Topic> GetTopics()
    {
        if (_cachedTopics != null)
            return _cachedTopics;

        _cachedTopics = typeof(Topic)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Topic))
            .Select(f => (Topic)f.GetValue(null)!)
            .ToList();

        return _cachedTopics;
    }

    /// <summary>
    /// Attempts to resolve a predefined <see cref="Topic"/> from its exact name
    /// without throwing. Topics form a closed enumeration whose names come from
    /// <see cref="GetTopics"/>; the match is deliberately case-sensitive so a
    /// mismatching client is reported instead of silently tolerated.
    /// </summary>
    /// <param name="name">The topic name to resolve.</param>
    /// <param name="topic">The resolved topic, or <see langword="null"/> when unknown.</param>
    /// <returns><see langword="true"/> when the name matches a predefined topic.</returns>
    public static bool TryFromName(string? name, [NotNullWhen(true)] out Topic? topic)
    {
        topic = GetTopics().FirstOrDefault(t => t.Name.Equals(name, StringComparison.Ordinal));
        return topic is not null;
    }

    public static Topic FromName(string name)
        => TryFromName(name, out var topic)
            ? topic
            : throw new ArgumentException($"Topic with name '{name}' does not exist.");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }
}
