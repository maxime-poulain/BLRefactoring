namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// A trainer as the application layer hands it back: already valid, no behaviour attached.
/// </summary>
public sealed class TrainerDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the API layer, which publishes it as an <c>ETag</c> and leaves it out of the
    /// response contract. This read model is no longer serialised to callers, so it no longer
    /// needs to say so.
    /// </remarks>
    public byte[] RowVersion { get; init; } = [];

    /// <summary>
    /// The identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The trainer's first name.
    /// </summary>
    public required string Firstname { get; init; } = null!;

    /// <summary>
    /// The trainer's last name.
    /// </summary>
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the
    /// email of their identity account.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The bio of the trainer, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }

    /// <summary>
    /// Identifies the trainer's photo, or <see langword="null"/> when they have none.
    /// </summary>
    /// <remarks>
    /// Not the bytes and not an address: a client that has this knows a photo exists and knows the
    /// value changes on every replacement, which is exactly what it needs to stop showing the
    /// previous one out of its cache.
    /// </remarks>
    public Guid? PhotoId { get; init; }

    /// <summary>
    /// Where the trainer stands: <c>Active</c> or <c>Suspended</c>.
    /// </summary>
    /// <remarks>
    /// Read by the trainer themselves and by nobody else — this model answers <c>/Trainer/me</c>.
    /// It is here because a sanction the product will not name to the person it lands on is one
    /// they can only discover by failing to do something (ADR 0053, ADR 0057).
    /// </remarks>
    public required string Status { get; init; }

    /// <summary>
    /// Why the trainer was suspended, or <see langword="null"/> when they are not.
    /// </summary>
    /// <remarks>
    /// Present if and only if <see cref="Status"/> is <c>Suspended</c> — the invariant ADR 0052
    /// states on the aggregate, carried out to the reader it is about.
    /// </remarks>
    public string? SuspensionReason { get; init; }
}
