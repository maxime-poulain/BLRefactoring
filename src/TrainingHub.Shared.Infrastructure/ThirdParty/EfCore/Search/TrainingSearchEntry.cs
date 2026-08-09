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
/// is what lets a suspension flip one column for a whole catalogue in a single statement without
/// forgetting which trainings were published (ADR 0056).
/// </para>
/// </remarks>
public sealed class TrainingSearchEntry
{
    private readonly List<TrainingSearchTerm> _terms = [];

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

    /// <summary>Whether the training itself is published.</summary>
    public bool IsPublished { get; private set; }

    /// <summary>Whether its owner's catalogue is out of public view.</summary>
    public bool IsTrainerHidden { get; private set; }

    /// <summary>The tokens this entry is found by.</summary>
    public IReadOnlyCollection<TrainingSearchTerm> Terms => _terms.AsReadOnly();

    /// <summary>
    /// Rewrites the entry from the document the indexer just read back.
    /// </summary>
    /// <param name="trainerId">The trainer the training is filed under.</param>
    /// <param name="title">The title as a visitor reads it.</param>
    /// <param name="isPublished">Whether the training itself is published.</param>
    /// <param name="isTrainerHidden">Whether its owner's catalogue is out of public view.</param>
    /// <param name="terms">The tokens the title yields.</param>
    /// <remarks>
    /// Total rather than incremental: the tokens are cleared and rewritten, so re-indexing a renamed
    /// training cannot leave it findable under the words of a title it no longer has.
    /// </remarks>
    public void Describe(
        Guid trainerId,
        string title,
        bool isPublished,
        bool isTrainerHidden,
        IEnumerable<string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        TrainerId = trainerId;
        Title = title;
        IsPublished = isPublished;
        IsTrainerHidden = isTrainerHidden;

        _terms.Clear();
        _terms.AddRange(terms.Select(term => new TrainingSearchTerm(TrainingId, term)));
    }
}
