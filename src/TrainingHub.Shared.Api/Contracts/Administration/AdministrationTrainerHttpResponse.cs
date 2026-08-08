namespace TrainingHub.Shared.Api.Contracts.Administration;

/// <summary>
/// A row of <c>GET /Administration/trainers</c>: a trainer, and where they stand.
/// </summary>
/// <remarks>
/// Its own contract rather than <c>TrainerHttpResponse</c> with two fields added, which is the
/// decision ADR 0055 records. The two are read by different audiences — that one is a trainer's own
/// profile, this one is a list only an administrator may read — and keeping them apart is what makes
/// it impossible to widen one and reach the other by accident.
/// <para>
/// What it leaves out says as much as what it carries. No bio, no photo, no <c>ETag</c>: an
/// administrator suspends and reinstates, and none of the three is part of that decision. A list
/// that read them would pay for columns nobody looks at, twenty rows at a time.
/// </para>
/// </remarks>
public sealed class AdministrationTrainerHttpResponse
{
    /// <summary>The trainer's identifier, which the administrative endpoints take.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer's first name.</summary>
    public required string Firstname { get; init; }

    /// <summary>The trainer's last name.</summary>
    public required string Lastname { get; init; }

    /// <summary>The address at which the trainer wishes to be contacted.</summary>
    public required string ContactEmail { get; init; }

    /// <summary><c>Active</c> or <c>Suspended</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why the trainer was suspended, or <see langword="null"/> when they are not.
    /// </summary>
    /// <remarks>
    /// Present if and only if <see cref="Status"/> is <c>Suspended</c> — the invariant ADR 0052
    /// states on the aggregate, carried out to the boundary rather than restated as a rule here.
    /// </remarks>
    public string? SuspensionReason { get; init; }
}
