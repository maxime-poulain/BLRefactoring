using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TrainingHub.Shared.Api.Contracts;

/// <summary>
/// Refuses a topic name the domain does not spell, on a filter a caller supplies.
/// </summary>
/// <remarks>
/// <see cref="KnownStatusAttribute"/>'s sibling, reflection included and for the same two reasons:
/// the allowed values are read off the domain type rather than restated — a hand-written list at
/// the boundary is the defect <c>EveryNamedList_AgreesWithTheCode</c> catches in prose (ADR 0041)
/// — and this layer names no domain type, which is a rule rather than a taste. The type is asked
/// for a public static <c>GetTopics()</c> and each answer for its <c>Name</c>, resolved once per
/// type and cached for the process.
/// <para>
/// The comparison is ordinal, because <c>Topic.TryFromName</c> is ordinal: what passes this
/// attribute is the domain's own canonical spelling, which is exactly what the search index stores,
/// and why a vetted name travels to it without translation (ADR 0069).
/// </para>
/// <para>
/// A null passes, since absence is <see cref="RequiredAttribute"/>'s question and a filter nobody
/// set is not a filter that failed. So does a blank string, which is what an empty query parameter
/// binds to — <c>?topic=</c> asks for no filter rather than for a topic called nothing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class KnownTopicAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> NamesByType = new();

    private readonly Type _topicType;

    /// <summary>
    /// Initializes the attribute against the domain type that owns the names.
    /// </summary>
    /// <param name="topicType">The topic value object — the one declaring <c>GetTopics()</c>.</param>
    public KnownTopicAttribute(Type topicType)
        : base("The {0} field must name a topic this catalog knows.")
    {
        _topicType = topicType;
    }

    /// <summary>
    /// Answers whether the value names a topic the domain declares.
    /// </summary>
    /// <param name="value">The bound value, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="false"/> only for a non-blank string that no topic answers to. Null and blank
    /// pass, and so does anything that is not a string — the framework convention for an attribute
    /// asked about a type it does not judge.
    /// </returns>
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string name when string.IsNullOrWhiteSpace(name) => true,
        string name => NamesOf(_topicType).Contains(name),
        _ => true
    };

    /// <summary>
    /// Every name the given topic type declares, compared as the domain compares them.
    /// </summary>
    private static IReadOnlySet<string> NamesOf(Type topicType) =>
        NamesByType.GetOrAdd(topicType, static type =>
        {
            var topics = type.GetMethod("GetTopics", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"{type.Name} declares no public static GetTopics(), so [KnownTopic] cannot " +
                    "read the names it is meant to refuse anything outside of.");

            var name = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"{type.Name} declares no public Name, so [KnownTopic] cannot read what its " +
                    "topics are called.");

            return ((IEnumerable)topics.Invoke(null, null)!)
                .Cast<object>()
                .Select(topic => (string)name.GetValue(topic)!)
                .ToHashSet(StringComparer.Ordinal);
        });
}
