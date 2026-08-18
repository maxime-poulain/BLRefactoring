using Microsoft.AspNetCore.Components.Authorization;
using TrainingHub.Blazor.Client.Authorization;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// One read of the caller's own profile, shared per scope, answering two questions: the standing
/// the banner renders, and the portrait address the user menu shows.
/// </summary>
/// <remarks>
/// Both answers come from the same <c>GET /Trainer/me</c>, so a second interface on the same
/// class is what keeps the layout from asking the API twice for one document.
/// <para>
/// It also decides, once, whether that read is worth making at all. An account that is nobody's
/// trainer has no standing and no portrait on this surface, and the API says so with a <c>403</c>
/// — so an administrator used to spend one refused call per scope to be told what their own
/// identity already said. Asking the identity first is what makes the refusal unnecessary rather
/// than merely handled, and it belongs here rather than at the five call sites: the banner, the
/// user menu and three pages all ask this class, and a guard repeated five times is four chances
/// to forget it (ADR 0078).
/// </para>
/// </remarks>
public sealed class TrainerStandingSource(
    ITrainerClient trainerClient,
    AuthenticationStateProvider authenticationStateProvider)
    : ITrainerStandingSource, ITrainerPortraitSource
{
    private const string Suspended = "Suspended";

    private Task<TrainerHttpResponse?>? _trainer;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public Task<TrainerStanding> GetAsync() => StandingAsync(_trainer ??= ReadAsync());

    /// <inheritdoc />
    public async Task<TrainerStanding> RefreshAsync()
    {
        _trainer = ReadAsync();

        var standing = await StandingAsync(_trainer);
        Changed?.Invoke();

        return standing;
    }

    /// <inheritdoc />
    public async Task<string?> FindOwnPortraitAsync()
    {
        var trainer = await (_trainer ??= ReadAsync());

        // The authenticated route, addressed by identifier with the photo's identity as a cache
        // buster — the same address the profile page builds (ADR 0063). No photo, or no trainer
        // behind the account, both answer nothing rather than a placeholder.
        return trainer is { PhotoId: not null }
            ? $"api/Trainer/{trainer.Id}/photo?v={trainer.PhotoId}"
            : null;
    }

    private static async Task<TrainerStanding> StandingAsync(Task<TrainerHttpResponse?> read)
    {
        var trainer = await read;

        if (trainer is null)
        {
            // Fail open on both flags: a failed read must neither accuse somebody of a sanction
            // nor nag somebody who already clicked their link (ADR 0057, ADR 0090).
            return TrainerStanding.Active;
        }

        return new TrainerStanding(
            IsSuspended: string.Equals(trainer.Status, Suspended, StringComparison.Ordinal),
            Reason: trainer.SuspensionReason,
            IsEmailVerified: trainer.IsEmailVerified);
    }

    private async Task<TrainerHttpResponse?> ReadAsync()
    {
        if (!await IsTrainerAsync())
        {
            // A visitor, or an account that is nobody's trainer. Either way this document is not
            // theirs to have, and the API would say so: the same question, asked where it costs
            // nothing instead of where it costs a round trip and a refusal.
            return null;
        }

        try
        {
            return (await trainerClient.GetCurrentAsync()).Result;
        }
        catch (Exception exception)
        {
            // The read failed, and a banner raised by a failed read would accuse somebody of a
            // sanction they may not be under. The generator's own sentence goes to the console,
            // never on screen. The caller's identity is checked above, so a 403 here means the
            // cookie and the token have drifted apart — which is a failure, not an administrator.
            // Wider than ApiException on purpose: an unreachable BFF surfaces here as
            // HttpRequestException, and every caller of this source is a page initializer that a
            // throw would kill outright.
            Console.Error.WriteLine(exception);

            return null;
        }
    }

    private async Task<bool> IsTrainerAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();

        return state.User.HasClaim(claim => claim.Type == SessionClaims.TrainerId);
    }
}
