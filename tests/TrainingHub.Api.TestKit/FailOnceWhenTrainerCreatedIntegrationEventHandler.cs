using System.Collections.Concurrent;
using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The failing neighbor production does not have: a test-only consumer of the trainer-created
/// fact that throws on its first delivery for a marked registration, and succeeds ever after.
/// </summary>
/// <remarks>
/// Registered by <see cref="ApiFactory{TEntryPoint}"/> after the production consumers, so the welcome email
/// has already been delivered when this one throws — which is exactly the interleaving the
/// isolation proof needs: the retry must skip the delivered neighbor and re-run only this
/// consumer (ADR 0034). It acts only on registrations whose first name is <see cref="Marker"/>
/// and is a silent no-op for everything else, so every other fact in the suites keeps its
/// first-attempt delivery and its <c>Attempts == 0</c>. A singleton, because the once-only
/// memory must survive the worker's per-batch scopes; keyed by contact email so parallel facts
/// and the fixture's lifetime cannot confuse one registration's first delivery with another's.
/// </remarks>
public sealed class FailOnceWhenTrainerCreatedIntegrationEventHandler
    : IIntegrationEventHandler<TrainerCreatedIntegrationEvent>
{
    /// <summary>The first name that opts a registration into one failed delivery.</summary>
    public const string Marker = "Fail-the-first-delivery";

    private readonly ConcurrentDictionary<string, bool> _alreadyFailed = new();

    /// <inheritdoc />
    public string ConsumerName => "TestKit.FailOnce";

    /// <inheritdoc />
    public Task HandleAsync(TrainerCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.Firstname == Marker && _alreadyFailed.TryAdd(integrationEvent.ContactEmail, true))
        {
            throw new InvalidOperationException(
                $"{ConsumerName} fails the first delivery for {integrationEvent.ContactEmail}, by design.");
        }

        return Task.CompletedTask;
    }
}
