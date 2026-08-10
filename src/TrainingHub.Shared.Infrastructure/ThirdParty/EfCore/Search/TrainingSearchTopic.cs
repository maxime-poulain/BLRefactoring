namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// One topic a training is filed under: the browse half of the index.
/// </summary>
/// <remarks>
/// The first dimension of this index that is not the title (ADR 0069). The tokens answer a visitor
/// who knows what they are looking for; these rows answer the one who does not — the catalog's
/// facets are counted from them, and a search narrowed to a topic is narrowed here.
/// <para>
/// The pair is the whole fact, so the pair is the key, exactly as the tokens' is (ADR 0059). The
/// name is stored as the domain spells it — <c>Topic</c>'s own canonical form, refused at every
/// boundary when it is anything else — so a facet needs no translation on the way out.
/// </para>
/// </remarks>
public sealed class TrainingSearchTopic
{
    /// <summary>
    /// Records that a training is filed under a topic.
    /// </summary>
    /// <param name="trainingId">The entry the topic belongs to.</param>
    /// <param name="topic">The topic's canonical name, as the domain spells it.</param>
    public TrainingSearchTopic(Guid trainingId, string topic)
    {
        TrainingId = trainingId;
        Topic = topic;
    }

    // EF Core materializes rows through this constructor; the public one is for the indexer.
    private TrainingSearchTopic() => Topic = string.Empty;

    /// <summary>The entry this topic belongs to — half of the identity.</summary>
    public Guid TrainingId { get; }

    /// <summary>The canonical topic name — the other half.</summary>
    public string Topic { get; }
}
