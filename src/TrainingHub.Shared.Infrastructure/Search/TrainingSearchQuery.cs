using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

namespace TrainingHub.Shared.Infrastructure.Search;

/// <inheritdoc />
public sealed class TrainingSearchQuery(TrainingContext trainingContext) : ITrainingSearchQuery
{
    /// <inheritdoc />
    /// <remarks>
    /// The term's half is <see cref="OfferedMatching"/>, shared with the facets so that both are
    /// read under one question. What this method adds is the shelves, and they compose the other
    /// way round: <em>every</em> word must match, <em>any</em> shelf will do (ADR 0080).
    /// <para>
    /// The filtering, the count and the page are one composed query, so the count describes the same
    /// set as the rows — the defect ADR 0055 names when it rejects filtering after paging.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        string? term,
        IReadOnlyCollection<string> topics,
        CatalogOrder order,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topics);

        var entries = OfferedMatching(term);

        if (topics.Count > 0)
        {
            // One EXISTS against the topics' own index, the same shape as a token's — but with an
            // IN inside it rather than an equality, and that single difference is the whole of
            // ADR 0080. Where OfferedMatching composes one EXISTS per word so that every word must
            // match, the shelves go into one so that any of them will do: a word the visitor adds
            // says more about what they want, a shelf they tick says they would also take what is
            // on it. Equality rather than a prefix inside it, because a topic is a name from a
            // closed set, not a word somebody is still typing (ADR 0069).
            entries = entries.Where(entry =>
                entry.Topics.Any(candidate => topics.Contains(candidate.Topic)));
        }

        // One of two named orders, both defined once in QueryableOrderingExtensions and both
        // total (ADR 0071) — never a sort expression a caller composed.
        var ordered = order == CatalogOrder.Newest
            ? entries.NewestOnOffer()
            : entries.AlphabeticallyByTitle();

        return await ordered
            .ToPagedResultAsync(
                entry => new CatalogTrainingDto
                {
                    Id = entry.TrainingId,
                    TrainerId = entry.TrainerId,
                    Title = entry.Title
                },
                paging,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A join and a count rather than sixteen counts: the topics table knows which entries it
    /// belongs to, the entries know which of them the term leaves standing, and the group answers
    /// every facet in one statement. Topics nothing matching declares simply have no rows to group,
    /// which is what the port's contract promises — absent, never zero. A term that empties a shelf
    /// therefore removes its chip rather than showing it at nought (ADR 0069, ADR 0080).
    /// </remarks>
    public async Task<IReadOnlyList<TopicFacetDto>> FacetsAsync(
        string? term,
        CancellationToken cancellationToken = default) =>
        await trainingContext.Set<TrainingSearchTopic>()
            .AsNoTracking()
            .Join(
                OfferedMatching(term),
                topic => topic.TrainingId,
                entry => entry.TrainingId,
                (topic, _) => topic.Topic)
            .GroupBy(topic => topic)
            .OrderBy(facet => facet.Key)
            .Select(facet => new TopicFacetDto
            {
                Topic = facet.Key,
                OfferedCount = facet.Count()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The offered entries the term leaves standing — the half of the question both the page and
    /// the facets are read under.
    /// </summary>
    /// <remarks>
    /// Written once rather than twice, because the two callers must narrow <em>identically</em>:
    /// a facet counted under a wider question than the search answers advertises a shelf that comes
    /// back empty, which is the one thing ADR 0069 asks these numbers not to do. Copying the token
    /// loop into the second caller would also be exactly the repetition ADR 0049's detector exists
    /// to see.
    /// <para>
    /// One <c>EXISTS</c> per token, composed rather than concatenated, so a training answers only
    /// when it matches <em>every</em> word: two words are a narrower question than one, and a search
    /// that widened as the caller typed would be a search that gets worse the more you tell it.
    /// That is the opposite of what the topics do to each other (ADR 0080), and the two live side by
    /// side on purpose.
    /// </para>
    /// <para>
    /// Each token is matched by prefix, which is the whole reason the tokens are stored: a prefix
    /// seeks along <c>IX_TrainingSearchTerm_Term</c>, where the <c>LIKE '%term%'</c> ADR 0055
    /// recorded scans every row by construction.
    /// </para>
    /// </remarks>
    private IQueryable<TrainingSearchEntry> OfferedMatching(string? term)
    {
        var entries = trainingContext.Set<TrainingSearchEntry>()
            .AsNoTracking()
            // What "on offer" means, composed exactly as the domain composes it (ADR 0050,
            // ADR 0056) — and stored, because the write side stores it nowhere.
            .Where(entry => entry.IsPublished && !entry.IsTrainerHidden);

        foreach (var token in SearchTerms.Of(term))
        {
            // The token is copied into a local the closure captures: the loop variable would be
            // captured by reference and every EXISTS would end up asking about the last word.
            var word = token;

            // A LIKE written out rather than StartsWith: the analyzer would demand a
            // StringComparison, and Ordinal is precisely what must not be asked for here — it forces
            // a binary collation on the column and turns the seek this index exists for back into a
            // scan. Nothing needs escaping, because a token is letters and digits by construction.
            entries = entries.Where(entry =>
                entry.Terms.Any(candidate => EF.Functions.Like(candidate.Term, word + "%")));
        }

        return entries;
    }
}
