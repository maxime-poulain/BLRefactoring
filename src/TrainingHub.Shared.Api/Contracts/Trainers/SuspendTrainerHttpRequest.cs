using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Trainers;

/// <summary>
/// The body of <c>POST /Administration/trainers/{trainerId}/suspend</c>.
/// </summary>
/// <remarks>
/// Only the reason is the caller's to send: the trainer comes from the route, and who is entitled
/// to sanction them is what <c>AdministratorPolicy</c> already settled. The bound mirrors
/// <c>SuspensionReason</c>'s, and mirroring is all it does — the value object stays the judge and
/// refuses on its own terms anything reaching it by another route (ADR 0052).
/// <para>
/// <c>[Required]</c> rather than a nullable property: a sanction without a stated reason is the one
/// thing ADR 0052 forbids, so the boundary refuses it before the domain has to.
/// </para>
/// </remarks>
public sealed class SuspendTrainerHttpRequest
{
    /// <summary>Why the trainer is being suspended.</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; init; } = null!;
}
