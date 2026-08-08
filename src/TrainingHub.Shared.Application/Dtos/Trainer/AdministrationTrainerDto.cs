namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// A trainer as the administration reads them: who they are, and where they stand.
/// </summary>
/// <remarks>
/// A second shape rather than fields added to <see cref="TrainerDto"/>, and the separation is the
/// decision (ADR 0055). The two answer different audiences — that one is a trainer's own profile,
/// this one is a row in a list nobody but an administrator may read — so a field added here can
/// never reach the trainer-facing response by accident, and the reverse.
/// <para>
/// It carries the standing and no bio, no photo and no row version: none of the three is something
/// the administration acts on, and a list that reads them pays for columns nobody looks at. What it
/// does carry that no other read does is <see cref="SuspensionReason"/> — present if and only if the
/// status is <c>Suspended</c>, which is the invariant ADR 0052 states on the aggregate and this
/// projection copies rather than reinterprets.
/// </para>
/// </remarks>
public sealed class AdministrationTrainerDto
{
    /// <summary>The trainer's identifier, which the administrative endpoints take.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer's first name.</summary>
    public required string Firstname { get; init; }

    /// <summary>The trainer's last name.</summary>
    public required string Lastname { get; init; }

    /// <summary>The address at which the trainer wishes to be contacted.</summary>
    public required string ContactEmail { get; init; }

    /// <summary>Whether the trainer is in good standing, or under sanction.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why the trainer was suspended, or <see langword="null"/> when they are not.
    /// </summary>
    public string? SuspensionReason { get; init; }
}
