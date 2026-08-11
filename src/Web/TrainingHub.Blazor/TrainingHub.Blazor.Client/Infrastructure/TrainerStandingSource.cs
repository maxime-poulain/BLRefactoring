using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// One read of the caller's own profile, shared per scope, answering two questions: the standing
/// the banner renders, and the portrait address the user menu shows.
/// </summary>
/// <remarks>
/// Both answers come from the same <c>GET /Trainer/me</c>, so a second interface on the same
/// class is what keeps the layout from asking the API twice for one document.
/// </remarks>
public sealed class TrainerStandingSource(ITrainerClient trainerClient)
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

        return trainer is not null
               && string.Equals(trainer.Status, Suspended, StringComparison.Ordinal)
            ? new TrainerStanding(IsSuspended: true, trainer.SuspensionReason)
            : TrainerStanding.Active;
    }

    private async Task<TrainerHttpResponse?> ReadAsync()
    {
        try
        {
            return (await trainerClient.GetCurrentAsync()).Result;
        }
        catch (ApiException exception)
        {
            // Two cases, one answer. A 403 means the caller is an account that is nobody's trainer —
            // an administrator — and has no standing and no portrait on this surface at all. Anything
            // else means the read failed, and a banner raised by a failed read would accuse somebody
            // of a sanction they may not be under. The generator's own sentence goes to the console,
            // never on screen.
            Console.Error.WriteLine(exception);

            return null;
        }
    }
}
