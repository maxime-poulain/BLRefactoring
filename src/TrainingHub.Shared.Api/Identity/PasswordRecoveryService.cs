using System.Transactions;
using Microsoft.AspNetCore.Identity;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// Runs the recovery flow of ADR 0084 over the store, the outbox and Identity's own machinery.
/// </summary>
/// <remarks>
/// The redeem side does its own credential bookkeeping and none of the credential writing: once
/// this repository's token is peeked, judged and atomically consumed, the password itself is
/// changed through <c>UserManager.ResetPasswordAsync</c> with a framework token minted and burned
/// inside this very call — so validators, hashing, security-stamp rotation and the concurrency
/// stamp all stay the framework's, and this class never touches a password hash.
/// </remarks>
public sealed class PasswordRecoveryService(
    UserManager<IdentityUser<Guid>> userManager,
    IPasswordResetTokenStore resetTokens,
    IAccountLanguageQuery accountLanguages,
    IIntegrationEventPublisher integrationEvents,
    IUnitOfWork unitOfWork) : IPasswordRecoveryService
{
    private static readonly PasswordResetOutcome.Succeeded Succeeded = new();
    private static readonly PasswordResetOutcome.LinkRefused LinkRefused = new();

    /// <inheritdoc />
    public async Task RequestAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        // The outbox row is the whole write, exactly like the contact form's (ADR 0082): there is
        // no aggregate to update, so the save is this flow's own. Deliberately no lookup here —
        // the request path must cost the same for every address it is given (ADR 0084).
        await integrationEvents.PublishAsync(
            new PasswordResetRequestedIntegrationEvent(emailAddress),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PasswordResetOutcome> ResetAsync(
        string emailAddress,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(emailAddress);

        if (user is null)
        {
            return LinkRefused;
        }

        // A look, not a redemption: judged before the password validators run, so a caller with a
        // junk token learns nothing from the password half — only the credential's owner ever
        // sees a password refused (ADR 0084).
        if (!await resetTokens.IsCurrentAsync(user.Id, token, cancellationToken))
        {
            return LinkRefused;
        }

        var rejections = new List<IdentityError>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var verdict = await validator.ValidateAsync(userManager, user, newPassword);

            if (!verdict.Succeeded)
            {
                rejections.AddRange(verdict.Errors);
            }
        }

        if (rejections.Count > 0)
        {
            // The link is deliberately still alive: a policy slip costs the visitor a retype, not
            // a trip back to their inbox.
            return new PasswordResetOutcome.PasswordRefused(rejections);
        }

        // The registration transaction's shape (ADR 0040): both contexts bind one connection
        // string, and the sequential opens share one enlisted connection — nothing promotes.
        // Everything from the consumption to the changed-password fact commits or rolls back
        // together, so a consumed credential always bought a changed password and a notice.
        using var transactionScope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        // The race settles here, on the row count of one guarded delete: of two concurrent
        // redemptions, exactly one gets past this line (ADR 0084).
        if (!await resetTokens.TryRedeemAsync(user.Id, token, cancellationToken))
        {
            return LinkRefused;
        }

        // Mint-and-burn: the framework's own reset token, generated and consumed inside this
        // request, so the password write goes through the front door — validators, hashing, and
        // the stamp rotations — while the token never leaves the process.
        var frameworkToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, frameworkToken, newPassword);

        if (!reset.Succeeded)
        {
            // A lost concurrency race, or an Identity refusal the validators above did not
            // predict. Answered like a dead link rather than explained: the scope rolls the
            // consumption back, so the caller may simply try their link again.
            return LinkRefused;
        }

        // Whoever owns the mailbox owns the account: a lockout earned by whoever was guessing
        // passwords has no business outliving the proof (ADR 0084).
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        var language = await accountLanguages.ByUserIdAsync(user.Id, cancellationToken);

        await integrationEvents.PublishAsync(
            new PasswordChangedIntegrationEvent(
                user.Id,
                user.Email ?? emailAddress,
                user.UserName ?? emailAddress,
                language),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        transactionScope.Complete();

        return Succeeded;
    }
}
