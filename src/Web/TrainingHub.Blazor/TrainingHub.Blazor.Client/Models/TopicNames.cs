namespace TrainingHub.Blazor.Client.Models;

/// <summary>
/// The one translation from a topic's value to the key its name is filed under.
/// </summary>
/// <remarks>
/// A topic is two things at once, and ADR 0091 keeps them apart. As a <em>value</em> it is the
/// English name: what the filter puts in the query string, what <c>[KnownTopic]</c> admits, what
/// the search index stores — and the front end passes it around untouched. As a <em>word</em> it
/// is whatever the visitor's language calls it, and that comes from <c>TopicResources</c>.
/// <para>
/// The key is derived rather than mapped, so a topic added to the closed set needs no entry here:
/// its own name in Pascal case, prefixed by the family. A census rule holds both directions
/// against the domain's set, so a name that stopped deriving to a declared key is a red build
/// rather than a screen showing <c>Topic_CloudComputing</c>.
/// </para>
/// </remarks>
public static class TopicNames
{
    /// <summary>The resource key under which a topic's name is translated.</summary>
    /// <param name="topic">The topic's value, as the API and the URL carry it.</param>
    public static string Key(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return "Topic_" + string.Concat(topic
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
