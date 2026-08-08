namespace TrainingHub.Shared;

/// <summary>
/// Outbound port keeping a read-side search index in sync with the trainings,
/// consumed by application-layer code such as domain event handlers.
/// Implementations live in the infrastructure layer.
/// </summary>
/// <remarks>
/// The port speaks primitives, like every port of this shared kernel: the search
/// engine sitting behind it knows nothing about the domain's typed identifiers.
/// </remarks>
public interface ITrainingSearchIndexer
{
    /// <summary>
    /// Creates or refreshes the search index entry of the given training.
    /// </summary>
    /// <param name="trainingId">The identifier of the training to (re)index.</param>
    /// <param name="trainerId">The identifier of the trainer owning the training.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task IndexAsync(Guid trainingId, Guid trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the given training from the search index, if it is there.
    /// </summary>
    /// <remarks>
    /// The operation this port went without, and whose absence meant a training that had been
    /// indexed stayed indexed after it was deleted — for ever, since nothing announced the
    /// deletion either. Withdrawing a training calls it too: an unpublished training is one the
    /// public must not be offered, and an index that still serves it makes the state a lie
    /// (ADR 0050).
    /// <para>
    /// Removing an entry that is not there is not an error. The caller is a consumer reading a
    /// committed fact, which may be delivered again after a lapsed lease, so this has to be safe
    /// to run twice.
    /// </para>
    /// </remarks>
    /// <param name="trainingId">The identifier of the training to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveAsync(Guid trainingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes everything a trainer offers out of public view, without forgetting it.
    /// </summary>
    /// <remarks>
    /// The index composes public visibility the way the domain does — a training is offered when it
    /// is published <em>and</em> its owner is in good standing (ADR 0050) — so a sanction is one
    /// call about a trainer rather than one call per training. Hiding rather than removing is what
    /// makes <see cref="ShowTrainerCatalogueAsync"/> cost a call too: an index that had deleted the
    /// entries would need the catalogue read back to rebuild them, for a sanction that wrote to
    /// none of them (ADR 0056).
    /// <para>
    /// Hiding what is already hidden, or a trainer the index has never heard of, is not an error:
    /// a committed fact may be delivered again after a lapsed lease.
    /// </para>
    /// </remarks>
    /// <param name="trainerId">The identifier of the trainer whose catalogue leaves public view.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task HideTrainerCatalogueAsync(Guid trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a trainer's catalogue back in public view, exactly as it was.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="HideTrainerCatalogueAsync"/>, and the reason that one hides
    /// instead of removing. Each training's own state is untouched by either call, so what comes
    /// back is what was published, never what was merely written (ADR 0056).
    /// </remarks>
    /// <param name="trainerId">The identifier of the trainer whose catalogue returns to public view.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ShowTrainerCatalogueAsync(Guid trainerId, CancellationToken cancellationToken = default);
}
