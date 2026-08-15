using TrainingHub.Shared;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.DDD.Application.Services.CatalogServices;

/// <summary>
/// Application service interface for the public catalog: five reads, and one write that changes
/// nothing.
/// </summary>
/// <remarks>
/// The layered stack's half of the query surface ADR 0059 opens on the Search Indexing context. It
/// is still the only application service here that drives no aggregate, and that is the point
/// rather than an omission: a catalog search reads a read model, and the write model has nothing to
/// say about it.
/// <para>
/// <see cref="ContactAsync"/> is the one member that is not a read, and the exception is narrower
/// than it looks: it changes no aggregate either. What it writes is one outbox row recording that a
/// visitor wrote to a trainer — a fact with no aggregate behind it, which is the shape ADR 0082
/// argues for. It answers a bare <c>Result</c> because it can be refused, where the reads cannot:
/// a trainer the catalog will not show is a trainer nobody may write to.
/// </para>
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
    /// The facets of the offered catalog under a term: each topic at least one matching training
    /// declares, with its count (ADR 0080).
    /// </summary>
    /// <param name="term">The term the facets are counted under, read as the search reads it.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<TopicFacetDto>> GetFacetsAsync(
        string? term,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Carries a visitor's message to the trainer they wrote to (ADR 0082).
    /// </summary>
    /// <remarks>
    /// It commits the fact and answers; the message leaves later, through the outbox, which is how
    /// every other email in this application is sent (ADR 0002, ADR 0031). So a caller learns that
    /// the message was accepted, never that it arrived — and that is the honest thing to tell them,
    /// because an SMTP server that is down when a visitor is on the page is not a reason to lose
    /// what they wrote.
    /// </remarks>
    /// <param name="request">The message, whole, and the trainer it is addressed to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<Result> ContactAsync(
        TrainerContactRequest request,
        CancellationToken cancellationToken = default);

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
    ICatalogDetailQuery catalogDetail,
    IIntegrationEventPublisher integrationEvents,
    IUnitOfWork unitOfWork)
    : ICatalogApplicationService
{
    /// <inheritdoc />
    public async Task<Result> ContactAsync(
        TrainerContactRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A bot is answered exactly as a person is, minus the sending: telling it apart is what
        // would teach it to try again with the field left blank (ADR 0082).
        if (request.LooksAutomated)
        {
            return Result.Success();
        }

        // Offered or invisible, the same predicate the profile answers under: a trainer the catalog
        // will not show a visitor is a trainer that visitor may not write to either. It is the
        // index's composed visibility rather than a second one written here, which is what
        // ADR 0062 asks of every public read — and this write reads before it writes.
        var trainer = await catalogDetail.FindOfferedTrainerAsync(request.TrainerId, cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(
                ErrorCodes.NotFound,
                $"No offered trainer with id `{request.TrainerId}` could be found.");
        }

        await integrationEvents.PublishAsync(
            new TrainerContactedIntegrationEvent(
                request.TrainerId,
                request.TrainingId,
                request.SenderFirstname,
                request.SenderLastname,
                request.SenderEmailAddress,
                request.Message),
            cancellationToken);

        // The outbox row is the whole write, and it needs a save of its own: nothing else in this
        // request touched the unit of work, so there is no aggregate's commit to ride along with
        // (ADR 0002, ADR 0082).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await trainingSearch.SearchAsync(
            request.Term, request.Topics, request.Order, request.Paging, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopicFacetDto>> GetFacetsAsync(
        string? term,
        CancellationToken cancellationToken = default) =>
        await trainingSearch.FacetsAsync(term, cancellationToken);

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
