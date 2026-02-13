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

    public static Topic FromName(string name)
    {
        var topic = GetTopics().FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return topic == null ? throw new ArgumentException($"Topic with name '{name}' does not exist.") : topic;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }
}
