using Microsoft.AspNetCore.Identity;

namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// What redeeming a reset link came to: it worked, the link was refused, or the link was fine
/// and the password was not.
/// </summary>
/// <remarks>
/// Three cases and deliberately not four or five. Every way a <em>link</em> can fail — unknown
/// address, wrong token, expired, already used, superseded, lost race — collapses into
/// <see cref="LinkRefused"/> before it leaves the service, so no caller can turn the difference
/// into an oracle for which accounts exist (ADR 0084). A refused <em>password</em> is the one
/// distinguishable failure, and only a caller holding a valid link — the account's owner — ever
/// reaches it.
/// </remarks>
public abstract record PasswordResetOutcome
{
    private PasswordResetOutcome()
    {
    }

    /// <summary>
    /// The password was changed and the credential consumed.
    /// </summary>
    public sealed record Succeeded : PasswordResetOutcome;

    /// <summary>
    /// The link bought nothing, for a reason this type refuses to name.
    /// </summary>
    public sealed record LinkRefused : PasswordResetOutcome;

    /// <summary>
    /// The link was current and the password broke the policy — the link survives, so the owner
    /// corrects the password without going back to their inbox.
    /// </summary>
    /// <param name="Errors">What the password validators refused, in Identity's own words.</param>
    public sealed record PasswordRefused(IReadOnlyCollection<IdentityError> Errors) : PasswordResetOutcome;
}
