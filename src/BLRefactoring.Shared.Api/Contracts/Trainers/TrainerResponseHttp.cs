namespace BLRefactoring.Shared.Api.Contracts.Trainers;

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
public sealed class TrainerResponseHttp
{
    public required Guid Id { get; init; }

    public required string Firstname { get; init; }

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
}
