namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// One token a training is findable by: the row that makes this an index rather than a copy.
/// </summary>
/// <remarks>
/// The inverted half of the read model (ADR 0059). A title kept whole can only be searched with a
/// leading wildcard, which no index can seek — the cost ADR 0055 recorded and paid on the trainers'
/// listing. Split into tokens and indexed by token, the same search becomes a seek per word.
/// <para>
/// The pair is the whole fact, so the pair is the key, exactly as the delivery ledger's is
/// (ADR 0034). The token is stored upper-cased so that a match needs no collation to agree with it.
/// </para>
/// </remarks>
public sealed class TrainingSearchTerm
{
    /// <summary>
    /// Records that a training is findable by a token.
    /// </summary>
    /// <param name="trainingId">The entry the token belongs to.</param>
    /// <param name="term">The token, already normalized.</param>
    public TrainingSearchTerm(Guid trainingId, string term)
    {
        TrainingId = trainingId;
        Term = term;
    }

    // EF Core materializes rows through this constructor; the public one is for the indexer.
    private TrainingSearchTerm() => Term = string.Empty;

    /// <summary>The entry this token belongs to — half of the identity.</summary>
    public Guid TrainingId { get; }

    /// <summary>The normalized token — the other half.</summary>
    public string Term { get; }
}
