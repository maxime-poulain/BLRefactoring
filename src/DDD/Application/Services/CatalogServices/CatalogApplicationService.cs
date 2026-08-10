using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.DDD.Application.Services.CatalogServices;

/// <summary>
/// Application service interface for the public catalog: reads only, and no writes at all.
/// </summary>
/// <remarks>
/// The layered stack's half of the query surface ADR 0059 opens on the Search Indexing context. It
/// is the only application service here that drives no aggregate, and that is the point rather than
/// an omission: a catalog search reads a read model, and the write model has nothing to say about
/// it. It is also why this one has no <c>Result</c> anywhere — there is no rule to break.
/// </remarks>
public interface ICatalogApplicationService
{
    /// <summary>
    /// One page of the offered catalog, narrowed by a term and a topic when there are any, in one
    /// of the catalog's two published orders (ADR 0071).
    /// </summary>
    /// <param name="request">
    /// The question, whole: term, topic, order and page. The boundary refuses a topic the domain
    /// does not spell and an order the catalog does not publish before this service sees either
    /// (ADR 0069, ADR 0071).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The facets of the offered catalog: each topic at least one offered training declares, with
    /// its count.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<TopicFacetDto>> GetFacetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One offered training in full, or <see langword="null"/> when there is none a visitor may see.
    /// </summary>
    /// <remarks>
    /// The second read of this service, and the first that is not a search — which is why it goes
    /// to a port of its own rather than to <c>ITrainingSearchQuery</c>. What makes it safe is the
    /// same thing that makes the search safe: the index decides what is on offer, and this service
    /// never asks the write model that question (ADR 0062).
    /// </remarks>
    /// <param name="trainingId">The training the visitor asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CatalogTrainingDetailDto?> FindOfferedAsync(
        Guid trainingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The portrait of the trainer behind an offered training, or <see langword="null"/> when there
    /// is none a visitor may see.
    /// </summary>
    /// <remarks>
    /// The only read in this application that answers bytes to nobody in particular, and it is safe
    /// for two reasons at once: the index decides whether the training is on offer, and the write
    /// model refuses a portrait that carries no sanitization stamp (ADR 0063).
    /// </remarks>
    /// <param name="trainingId">The offered training the visitor is looking at.</param>
    /// <param name="photoId">The photo its address names.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<TrainerPhotoDto?> FindOfferedPortraitAsync(
        Guid trainingId,
        Guid photoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The public profile of an offering trainer, or <see langword="null"/> when there is none a
    /// visitor may see.
    /// </summary>
    /// <remarks>
    /// Offered or invisible: the index decides whether this person is offering anything, exactly
    /// as it decides whether a training is on offer, and this service never asks the write model
    /// that question (ADR 0062, ADR 0070).
    /// </remarks>
    /// <param name="trainerId">The trainer the visitor asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CatalogTrainerDto?> FindOfferedTrainerAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The portrait of an offering trainer, or <see langword="null"/> when there is none a visitor
    /// may see.
    /// </summary>
    /// <remarks>
    /// The profile's own address for the same bytes the training-addressed read serves: each page
    /// asks with what it has in hand, and both answers are cacheable forever because both name the
    /// photo (ADR 0063, ADR 0070).
    /// </remarks>
    /// <param name="trainerId">The offering trainer the visitor is looking at.</param>
    /// <param name="photoId">The photo its address names.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<TrainerPhotoDto?> FindTrainerPortraitAsync(
        Guid trainerId,
        Guid photoId,
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
public sealed class CatalogApplicationService(
    ITrainingSearchQuery trainingSearch,
    ICatalogDetailQuery catalogDetail)
    : ICatalogApplicationService
{
    /// <inheritdoc />
    public async Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await trainingSearch.SearchAsync(
            request.Term, request.Topic, request.Order, request.Paging, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopicFacetDto>> GetFacetsAsync(
        CancellationToken cancellationToken = default) =>
        await trainingSearch.FacetsAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<CatalogTrainingDetailDto?> FindOfferedAsync(
        Guid trainingId,
        CancellationToken cancellationToken = default) =>
        await catalogDetail.FindOfferedAsync(trainingId, cancellationToken);

    /// <inheritdoc />
    public async Task<TrainerPhotoDto?> FindOfferedPortraitAsync(
        Guid trainingId,
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        await catalogDetail.FindOfferedPortraitAsync(trainingId, photoId, cancellationToken);

    /// <inheritdoc />
    public async Task<CatalogTrainerDto?> FindOfferedTrainerAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default) =>
        await catalogDetail.FindOfferedTrainerAsync(trainerId, cancellationToken);

    /// <inheritdoc />
    public async Task<TrainerPhotoDto?> FindTrainerPortraitAsync(
        Guid trainerId,
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        await catalogDetail.FindTrainerPortraitAsync(trainerId, photoId, cancellationToken);
}
