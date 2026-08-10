namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// One training as the search index holds it: what a visitor would read, and whether they may.
/// </summary>
/// <remarks>
/// The read model of the Search Indexing context (ADR 0059), and a persistence shape rather than a
/// domain one — no aggregate, no audit, no events. Its identity is the training's, so the upsert the
/// port promises converges on one row however often a fact is replayed.
/// <para>
/// Visibility is stored as the two facts it is composed of rather than as their conjunction. The
/// write side stores neither: a training carries its own status, its owner carries their standing,
/// and "offered to the public" lives in the space between them (ADR 0050). Keeping the halves apart
/// is what lets a suspension flip one column for a whole catalog in a single statement without
/// forgetting which trainings were published (ADR 0056).
/// </para>
/// </remarks>
public sealed class TrainingSearchEntry
{
    private readonly List<TrainingSearchTerm> _terms = [];

    private readonly List<TrainingSearchTopic> _topics = [];

    /// <summary>
    /// Opens an entry for a training the index has not heard of yet.
    /// </summary>
    /// <param name="trainingId">The training this entry describes.</param>
    public TrainingSearchEntry(Guid trainingId)
    {
        TrainingId = trainingId;
        Title = string.Empty;
    }

    // EF Core materializes rows through this constructor; the public one is for the indexer.
    private TrainingSearchEntry() => Title = string.Empty;

    /// <summary>The training this entry describes, and the entry's whole identity.</summary>
    public Guid TrainingId { get; }

    /// <summary>The trainer the training is filed under, which a transfer changes.</summary>
    public Guid TrainerId { get; private set; }

    /// <summary>The title as a visitor reads it, unfolded and uncased.</summary>
    public string Title { get; private set; }

    /// <summary>
    /// When the training was created — the training's own age, read back from the write model.
    /// </summary>
    /// <remarks>
    /// A fact about the training rather than about this read model: the day the indexer happened
    /// to write this row would reshuffle after every replay, where the training's age never moves
    /// (ADR 0071). It is what "newest first" orders on.
    /// </remarks>
    public DateTime CreatedOnUtc { get; private set; }

    /// <summary>Whether the training itself is published.</summary>
    public bool IsPublished { get; private set; }

    /// <summary>Whether its owner's catalog is out of public view.</summary>
    public bool IsTrainerHidden { get; private set; }

    /// <summary>The tokens this entry is found by.</summary>
    public IReadOnlyCollection<TrainingSearchTerm> Terms => _terms.AsReadOnly();

    /// <summary>The topics this entry is filed under.</summary>
    public IReadOnlyCollection<TrainingSearchTopic> Topics => _topics.AsReadOnly();

    /// <summary>
    /// Rewrites the entry from the document the indexer just read back.
    /// </summary>
    /// <param name="trainerId">The trainer the training is filed under.</param>
    /// <param name="title">The title as a visitor reads it.</param>
    /// <param name="createdOnUtc">When the training was created, from the write model.</param>
    /// <param name="isPublished">Whether the training itself is published.</param>
    /// <param name="isTrainerHidden">Whether its owner's catalog is out of public view.</param>
    /// <param name="terms">The tokens the title yields.</param>
    /// <param name="topics">The canonical names of the topics the training declares.</param>
    /// <remarks>
    /// Total rather than incremental: the tokens and the topics are cleared and rewritten, so
    /// re-indexing an edited training cannot leave it findable under the words of a title it no
    /// longer has — or filed under a topic it no longer declares.
    /// </remarks>
    public void Describe(
        Guid trainerId,
        string title,
        DateTime createdOnUtc,
        bool isPublished,
        bool isTrainerHidden,
        IEnumerable<string> terms,
        IEnumerable<string> topics)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(topics);

        TrainerId = trainerId;
        Title = title;
        CreatedOnUtc = createdOnUtc;
        IsPublished = isPublished;
        IsTrainerHidden = isTrainerHidden;

        _terms.Clear();
        _terms.AddRange(terms.Select(term => new TrainingSearchTerm(TrainingId, term)));

        _topics.Clear();
        _topics.AddRange(topics.Select(topic => new TrainingSearchTopic(TrainingId, topic)));
    }
}
