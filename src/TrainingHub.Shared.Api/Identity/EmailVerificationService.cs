using System.Transactions;
using Microsoft.AspNetCore.Identity;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// Runs the verification flow of ADR 0090 over the store, the outbox and Identity's own machinery.
/// </summary>
/// <remarks>
/// <see cref="PasswordRecoveryService"/>'s shape, one credential over. The redeem side does its
/// own credential bookkeeping and none of the flag writing: once this repository's token is
/// atomically consumed, <c>EmailConfirmed</c> is set through
/// <c>UserManager.ConfirmEmailAsync</c> with a framework token minted and burned inside this very
/// call — so the write goes through the front door, concurrency stamp and all, and this class
/// never touches the column.
/// </remarks>
public sealed class EmailVerificationService(
    UserManager<IdentityUser<Guid>> userManager,
    IEmailVerificationTokenStore verificationTokens,
    IIntegrationEventPublisher integrationEvents,
    IUnitOfWork unitOfWork) : IEmailVerificationService
{
    /// <inheritdoc />
    public async Task RequestAsync(Guid userId, string culture, CancellationToken cancellationToken = default)
    {
        // The outbox row is the whole write, exactly like the recovery request's (ADR 0084):
        // there is no aggregate to update, so the save is this flow's own. No lookup here either
        // — an already-verified account asking again is absorbed by the consumer's null mint, so
        // the request path does the same work every time (ADR 0090).
        await integrationEvents.PublishAsync(
            new EmailVerificationRequestedIntegrationEvent(userId, culture),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        // The consumption and the proof commit or roll back together — the reset flow's
        // transaction, one credential over (ADR 0084, ADR 0040's shape): both contexts bind one
        // connection string, so nothing promotes.
        using var transactionScope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        // The race settles here, on the row count of one guarded delete: of two concurrent
        // clicks, exactly one gets past this line (ADR 0084, ADR 0090).
        var userId = await verificationTokens.TryRedeemAsync(token, cancellationToken);

        if (userId is null)
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());

        if (user is null)
        {
            // The account left between the mint and the click; the cascade has just not caught up
            // with this transaction's view. There is nothing to prove.
            return false;
        }

        // Mint-and-burn: the framework's own confirmation token, generated and consumed inside
        // this request, so the flag write stays behind Identity's validators and stamps while the
        // token never leaves the process.
        var frameworkToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmation = await userManager.ConfirmEmailAsync(user, frameworkToken);

        if (!confirmation.Succeeded)
        {
            // A lost concurrency race, most likely. Answered like a dead link rather than
            // explained: the scope rolls the consumption back, so the visitor may simply try
            // their link again — a good link is never burned on a failure.
            return false;
        }

        transactionScope.Complete();

        return true;
    }
}
