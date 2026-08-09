using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.DDD.Application.Services.CatalogueServices;

/// <summary>
/// Application service interface for the public catalogue: one read, and no writes at all.
/// </summary>
/// <remarks>
/// The layered stack's half of the query surface ADR 0059 opens on the Search Indexing context. It
/// is the only application service here that drives no aggregate, and that is the point rather than
/// an omission: a catalogue search reads a read model, and the write model has nothing to say about
/// it. It is also why this one has no <c>Result</c> anywhere — there is no rule to break.
/// </remarks>
public interface ICatalogueApplicationService
{
    /// <summary>
    /// One page of the offered catalogue, narrowed by a term when there is one.
    /// </summary>
    /// <param name="term">What to look for, or nothing at all.</param>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PagedResult<CatalogueTrainingDto>> SearchAsync(
        string? term,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
/// <remarks>
/// A pass-through, and deliberately not hidden as one. The two stacks differ where the write model
/// gives them something to differ about — one drives aggregates through a service, the other sends
/// a command to a handler. Over a read model there is nothing to differ about: both ask the index
/// the one question it answers, so both arrive at the same port, and inventing a second reading of
/// the same rows to make the halves look different would be the duplication ADR 0049 measures.
/// </remarks>
public sealed class CatalogueApplicationService(ITrainingSearchQuery trainingSearch)
    : ICatalogueApplicationService
{
    /// <inheritdoc />
    public async Task<PagedResult<CatalogueTrainingDto>> SearchAsync(
        string? term,
        PageRequest paging,
        CancellationToken cancellationToken = default) =>
        await trainingSearch.SearchAsync(term, paging, cancellationToken);
}
