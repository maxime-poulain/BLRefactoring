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
    /// One <c>EXISTS</c> per token, composed rather than concatenated, so a training answers only
    /// when it matches <em>every</em> word: two words are a narrower question than one, and a search
    /// that widened as the caller typed would be a search that gets worse the more you tell it.
    /// <para>
    /// Each token is matched by prefix, which is the whole reason the tokens are stored: a prefix
    /// seeks along <c>IX_TrainingSearchTerm_Term</c>, where the <c>LIKE '%term%'</c> ADR 0055
    /// recorded scans every row by construction.
    /// </para>
    /// <para>
    /// The filtering, the count and the page are one composed query, so the count describes the same
    /// set as the rows — the defect ADR 0055 names when it rejects filtering after paging.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        string? term,
        string? topic,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var entries = trainingContext.Set<TrainingSearchEntry>()
            .AsNoTracking()
            // What "on offer" means, composed exactly as the domain composes it (ADR 0050,
            // ADR 0056) — and stored, because the write side stores it nowhere.
            .Where(entry => entry.IsPublished && !entry.IsTrainerHidden);

        if (topic is not null)
        {
            // One EXISTS against the topics' own index, the same shape as a token's. Equality
            // rather than a prefix, because a topic is a name from a closed set, not a word
            // somebody is still typing (ADR 0069).
            entries = entries.Where(entry =>
                entry.Topics.Any(candidate => candidate.Topic == topic));
        }

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

        return await entries
            .AlphabeticallyByTitle()
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
    /// A join and a count rather than six counts: the topics table knows which entries it belongs
    /// to, the entries know which of them are on offer, and the group answers every facet in one
    /// statement. Topics nothing offered declares simply have no rows to group, which is what the
    /// port's contract promises — absent, never zero.
    /// </remarks>
    public async Task<IReadOnlyList<TopicFacetDto>> FacetsAsync(
        CancellationToken cancellationToken = default) =>
        await trainingContext.Set<TrainingSearchTopic>()
            .AsNoTracking()
            .Join(
                trainingContext.Set<TrainingSearchEntry>()
                    .Where(entry => entry.IsPublished && !entry.IsTrainerHidden),
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
}
