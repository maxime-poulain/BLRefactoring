namespace TrainingHub.Shared.Api.Contracts.Trainers;

/// <summary>
/// A trainer as the API publishes it.
/// </summary>
/// <remarks>
/// Distinct from the application layer's <c>TrainerDto</c>, which both stacks read from the
/// database. That one is a read model, free to gain a column or drop one; this one is a promise
/// made to callers.
/// <para>
/// No version property: the aggregate's row version leaves in the <c>ETag</c> header, which is
/// where a transport concern belongs. <c>TrainerDto</c> used to carry it under a
/// <c>[JsonIgnore]</c> precisely because it doubled as a response body; now that it no longer
/// does, the attribute is gone with the coupling that required it.
/// </para>
/// </remarks>
public sealed class TrainerHttpResponse
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The trainer's first name.
    /// </summary>
    public required string Firstname { get; init; }

    /// <summary>
    /// The trainer's last name.
    /// </summary>
    public required string Lastname { get; init; }

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the email of their
    /// identity account.
    /// </summary>
    public required string ContactEmail { get; init; }

    /// <summary>
    /// The trainer's bio, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }

    /// <summary>
    /// Identifies the trainer's photo, or <see langword="null"/> when they have none.
    /// </summary>
    /// <remarks>
    /// The photo itself is at <c>GET /Trainer/{id}/photo</c>, which is cached hard because the
    /// bytes under it never change. This value does change on every replacement, so a client that
    /// appends it to that address gets the new portrait instead of the one it already has.
    /// </remarks>
    public Guid? PhotoId { get; init; }
}
