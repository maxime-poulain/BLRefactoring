using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

namespace TrainingHub.Shared.Infrastructure.Search;

/// <summary>
/// The search index, kept in two tables of the same database (ADR 0059).
/// </summary>
/// <remarks>
/// The adapter that replaced the fake. It is the only writer of either table, which is what lets
/// this class alone decide what a document is: an entry a visitor reads, and the tokens it is found
/// by. Every operation is safe to run twice, because every caller is an outbox consumer reading a
/// committed fact that a lapsed lease may deliver again (ADR 0025, ADR 0034).
/// <para>
/// The port carries two identifiers and no document, so the document is read back here. That is one
/// read for the one training a fact just spoke about — not the per-training rebuild ADR 0056
/// rejected for a sanction, which is why the sanction's two operations below still touch no
/// training row at all.
/// </para>
/// </remarks>
public sealed class TrainingSearchIndexer(TrainingContext trainingContext) : ITrainingSearchIndexer
{
    /// <inheritdoc />
    /// <remarks>
    /// A training the write model no longer holds is indexed as nothing: the deletion's own fact
    /// carries the removal, and inventing an entry here would race it into existence again.
    /// </remarks>
    public async Task IndexAsync(
        Guid trainingId,
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var document = await ReadDocumentAsync(trainingId, trainerId, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return;
        }

        var entry = await trainingContext.Set<TrainingSearchEntry>()
            .Include(candidate => candidate.Terms)
            .FirstOrDefaultAsync(candidate => candidate.TrainingId == trainingId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            entry = new TrainingSearchEntry(trainingId);
            trainingContext.Set<TrainingSearchEntry>().Add(entry);
        }

        entry.Describe(
            document.TrainerId,
            document.Title,
            document.IsPublished,
            document.IsTrainerHidden,
            SearchTerms.Of(document.Title));

        await trainingContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid trainingId, CancellationToken cancellationToken = default) =>
        // The tokens go with the entry through the cascading foreign key, in the database engine,
        // with no tracker involved — the same shape the outbox's ledger rides (ADR 0034).
        await trainingContext.Set<TrainingSearchEntry>()
            .Where(entry => entry.TrainingId == trainingId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task HideTrainerCatalogAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default) =>
        await SetCatalogHidden(trainerId, hidden: true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ShowTrainerCatalogAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default) =>
        await SetCatalogHidden(trainerId, hidden: false, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Flips the visibility of a whole catalog in one statement.
    /// </summary>
    /// <remarks>
    /// What ADR 0056 promised a real adapter would do: one call about a trainer, one statement in
    /// the database, and each training's own publication untouched — so a lifting puts back exactly
    /// what was published and never what was merely written.
    /// </remarks>
    private async Task SetCatalogHidden(
        Guid trainerId,
        bool hidden,
        CancellationToken cancellationToken) =>
        await trainingContext.Set<TrainingSearchEntry>()
            .Where(entry => entry.TrainerId == trainerId)
            .ExecuteUpdateAsync(
                entry => entry.SetProperty(candidate => candidate.IsTrainerHidden, hidden),
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Reads back what the index needs to hold, or nothing when the training is gone.
    /// </summary>
    /// <remarks>
    /// The owner's standing is read from the trainer the fact names rather than from the entry that
    /// may already exist, because a transfer can move a training into a hidden catalog — and back
    /// out of one.
    /// </remarks>
    private async Task<SearchDocument?> ReadDocumentAsync(
        Guid trainingId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        // The typed identifiers refuse an empty Guid, and this runs nowhere near the validation
        // pipeline that turns a malformed request into a 400 — the trap TrainingOwnerQuery met.
        if (trainingId == Guid.Empty || trainerId == Guid.Empty)
        {
            return null;
        }

        var id = TrainingId.Create(trainingId);
        var owner = TrainerId.Create(trainerId);

        // Two columns rather than the aggregate: the write model is not this adapter's to load, and
        // an index that held a Training would be tempted to ask it questions.
        var training = await trainingContext.Trainings
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                Title = candidate.Title.Value,
                IsPublished = candidate.Status == TrainingStatus.Published
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (training is null)
        {
            return null;
        }

        var isTrainerHidden = await trainingContext.Trainers
            .AsNoTracking()
            .AnyAsync(
                trainer => trainer.Id == owner && trainer.Status == TrainerStatus.Suspended,
                cancellationToken)
            .ConfigureAwait(false);

        return new SearchDocument(trainerId, training.Title, training.IsPublished, isTrainerHidden);
    }

    /// <summary>What one fact makes the index know about one training.</summary>
    private sealed record SearchDocument(
        Guid TrainerId,
        string Title,
        bool IsPublished,
        bool IsTrainerHidden);
}
