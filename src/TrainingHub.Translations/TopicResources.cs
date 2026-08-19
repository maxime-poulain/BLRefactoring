namespace TrainingHub.Translations;

/// <summary>
/// Marker for the sixteen subjects a training can be about, said in each language.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;TopicResources&gt;</c> uses the
/// type to find the resource files sitting beside it, one per language, and that is its whole job
/// (ADR 0088). A family of its own because a topic is a closed set the domain owns, and the two
/// directions are held by a census rule — every topic has a key, every key names a topic
/// (ADR 0091).
/// <para>
/// The keys are the topic's own name in Pascal case, prefixed: <c>Topic_DataAndAnalytics</c>. The
/// <em>values</em> that travel — the filter's query string, the contract's <c>[KnownTopic]</c>,
/// the rows of the search index — stay the English name, because a filter key is a value and not a
/// word. What a visitor reads and what a request carries are two different things on purpose.
/// </para>
/// </remarks>
public sealed class TopicResources;
