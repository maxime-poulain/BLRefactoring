using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// The closed set of subjects a training can be about.
/// </summary>
public sealed class Topic : ValueObject
{
    public static readonly Topic Programming = new("Programming");
    public static readonly Topic Design = new("Design");
    public static readonly Topic Marketing = new("Marketing");
    public static readonly Topic Business = new("Business");
    public static readonly Topic PersonalDevelopment = new("Personal Development");
    public static readonly Topic Leadership = new("Leadership");

    /// <summary>
    /// Every topic there is, in declaration order.
    /// </summary>
    /// <remarks>
    /// Declared rather than discovered. The set used to be reflected out of the static fields on
    /// first call and kept in a mutable <see cref="List{T}"/> that <c>GetTopics()</c> handed out
    /// as-is: any caller could clear the domain's closed enumeration for the lifetime of the
    /// process, since the cache is static. The lazy fill was unguarded too, so two threads
    /// arriving together each built their own copy.
    /// </remarks>
    private static readonly IReadOnlyList<Topic> All = ImmutableArray.Create(
        Programming, Design, Marketing, Business, PersonalDevelopment, Leadership);

    public string Name { get; init; } = null!;

    private Topic() { } // For ORM

    private Topic(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// The closed set of topics.
    /// </summary>
    public static IReadOnlyList<Topic> GetTopics() => All;

    /// <summary>
    /// Attempts to resolve a predefined <see cref="Topic"/> from its exact name
    /// without throwing. Topics form a closed enumeration whose names come from
    /// <see cref="GetTopics"/>; the match is deliberately case-sensitive so a
    /// mismatching client is reported instead of silently tolerated.
    /// </summary>
    /// <remarks>
    /// The only way in. A throwing <c>FromName</c> used to sit beside it, called from nowhere but
    /// the tests: an unrecognised name is a validation error the application layer reports along
    /// with everything else that was wrong, never an exception.
    /// </remarks>
    /// <param name="name">The topic name to resolve.</param>
    /// <param name="topic">The resolved topic, or <see langword="null"/> when unknown.</param>
    /// <returns><see langword="true"/> when the name matches a predefined topic.</returns>
    public static bool TryFromName(string? name, [NotNullWhen(true)] out Topic? topic)
    {
        topic = All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.Ordinal));
        return topic is not null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }
}
