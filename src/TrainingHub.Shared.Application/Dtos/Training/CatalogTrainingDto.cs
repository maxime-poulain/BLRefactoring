namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// A training as the public catalog reads it: what it is called, and whose it is.
/// </summary>
/// <remarks>
/// The third shape a training takes, and the smallest, because it is the only one that answers
/// nobody in particular. It carries what the search index holds and nothing else — no status, since
/// an entry a visitor can see is on offer by construction; no reason, since the states that carry
/// one are exactly the states the index does not serve; no content, since a search result is a way
/// of finding a training rather than a way of reading it (ADR 0059).
/// <para>
/// A separate shape rather than <see cref="TrainingDto"/> narrowed, for ADR 0055's reason: an
/// audience is what separates two contracts, and this one's audience is anybody at all.
/// </para>
/// </remarks>
public sealed class CatalogTrainingDto
{
    /// <summary>The training's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer who offers it.</summary>
    public required Guid TrainerId { get; init; }

    /// <summary>The training's title, as the index stored it.</summary>
    public required string Title { get; init; }
}
