using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Trainers;

/// <summary>
/// The body of <c>PUT /Trainer/me</c>: the new state of the caller's profile.
/// </summary>
/// <remarks>
/// The whole profile is replaced, so a <see langword="null"/> <see cref="Bio"/> clears the
/// current one.
/// <para>
/// It carries only what travels in the body. The trainer being edited comes from the token and
/// the expected version from <c>If-Match</c>; joining the three is the mapping's job. That
/// separation is the point of this type: the CQRS command used to be bound straight from the
/// body and then have those two fields assigned by the controller, which is why it needed
/// <c>[JsonIgnore]</c> — a serialization concern inside an application message.
/// </para>
/// <para>
/// The constraints are declared as attributes so that <c>[ApiController]</c> answers a
/// <c>ValidationProblemDetails</c> keyed by field name before the action runs — what a form on
/// the other end needs to mark the offending input rather than show one message for the whole
/// submission. They also reach the OpenAPI document, and therefore the generated clients.
/// </para>
/// <para>
/// They mirror the bounds the value objects enforce, and mirroring is all they do: the domain
/// stays the judge, and rejects on its own terms anything reaching it by another route. What is
/// deliberately absent is any check on the shape of the address — .NET's <c>[EmailAddress]</c>
/// and the domain's validator disagree, notably on a quoted local part containing an <c>@</c>,
/// and an API that refuses what the domain accepts would be worse than one that asks later.
/// </para>
/// </remarks>
public sealed class EditTrainerHttpRequest
{
    /// <summary>
    /// The trainer's first name, as the caller sent it.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Firstname { get; init; } = null!;

    /// <summary>
    /// The trainer's last name, as the caller sent it.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Editing it has no effect on the
    /// identity account used to sign in.
    /// </summary>
    [Required]
    public string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The new bio, or <see langword="null"/> to clear it.
    /// </summary>
    [StringLength(500)]
    public string? Bio { get; init; }
}
