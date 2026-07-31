namespace BLRefactoring.Shared.Api.Contracts.Trainers;

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
/// <c>[JsonIgnore]</c> — a serialisation concern inside an application message.
/// </para>
/// <para>
/// No <c>required</c> modifier, deliberately. A missing field must reach the layer that decides
/// what is valid — the value objects on the layered stack, the FluentValidation validators on the
/// CQRS one — rather than be turned into a binding error by the framework, which would answer
/// with a payload neither stack produces.
/// </para>
/// </remarks>
public sealed class EditTrainerRequestHttp
{
    public string Firstname { get; init; } = null!;

    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Editing it has no effect on the
    /// identity account used to sign in.
    /// </summary>
    public string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The new bio, or <see langword="null"/> to clear it.
    /// </summary>
    public string? Bio { get; init; }
}
