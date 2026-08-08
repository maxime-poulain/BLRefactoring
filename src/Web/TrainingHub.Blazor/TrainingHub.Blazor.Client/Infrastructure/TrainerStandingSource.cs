using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <inheritdoc />
public sealed class TrainerStandingSource(ITrainerClient trainerClient) : ITrainerStandingSource
{
    private const string Suspended = "Suspended";

    private Task<TrainerStanding>? _standing;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public Task<TrainerStanding> GetAsync() => _standing ??= ReadAsync();

    /// <inheritdoc />
    public async Task<TrainerStanding> RefreshAsync()
    {
        _standing = ReadAsync();

        var standing = await _standing;
        Changed?.Invoke();

        return standing;
    }

    private async Task<TrainerStanding> ReadAsync()
    {
        try
        {
            var trainer = (await trainerClient.GetCurrentAsync()).Result;

            return string.Equals(trainer.Status, Suspended, StringComparison.Ordinal)
                ? new TrainerStanding(IsSuspended: true, trainer.SuspensionReason)
                : TrainerStanding.Active;
        }
        catch (ApiException exception)
        {
            // Two cases, one answer. A 403 means the caller is an account that is nobody's trainer —
            // an administrator — and has no standing on this surface at all. Anything else means the
            // read failed, and a banner raised by a failed read would accuse somebody of a sanction
            // they may not be under. The generator's own sentence goes to the console, never on
            // screen.
            Console.Error.WriteLine(exception);

            return TrainerStanding.Active;
        }
    }
}
