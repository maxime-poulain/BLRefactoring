using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>POST /Administration/trainings/{trainingId}/withhold</c>.
/// </summary>
/// <remarks>
/// Only the reason is the caller's to send: the training comes from the route, and who is entitled
/// to withhold it is what <c>AdministratorPolicy</c> already settled. The bound mirrors
/// <c>WithholdingReason</c>'s, and mirroring is all it does — the value object stays the judge and
/// refuses on its own terms anything reaching it by another route (ADR 0052).
/// <para>
/// <c>[Required]</c> rather than a nullable property: withholding without a stated reason is the one
/// thing ADR 0052 forbids, so the boundary refuses it before the domain has to. The reason outlives
/// the request — it is read back by the trainer whose training was taken down, which is why it is
/// persisted beside the state rather than left on the event.
/// </para>
/// </remarks>
public sealed class WithholdTrainingHttpRequest
{
    /// <summary>Why the training is being withheld.</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; init; } = null!;
}
