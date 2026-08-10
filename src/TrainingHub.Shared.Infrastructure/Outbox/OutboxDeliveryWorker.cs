using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// The outbox's read side: a hosted service that drains the table on a timer and delivers each
/// message to its consumers — the worker ADR 0002 promised and ADR 0025 shapes.
/// </summary>
/// <remarks>
/// One instance runs in each API host, competing over the same table; the claim's lease and
/// <c>READPAST</c> make that a feature — two hosts drain twice as fast — rather than a race. Each
/// batch runs in its own service scope, as a request would, so the processor gets the same scoped
/// <c>TrainingContext</c> wiring the rest of the application uses. The loop survives everything
/// except shutdown: a failing poll is logged and retried at the next tick, because a delivery
/// worker that dies on the first database hiccup delivers nothing at all.
/// </remarks>
public sealed class OutboxDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDeliveryWorker> logger) : BackgroundService
{
    // Stable for this worker's lifetime, unique across restarts: which claimant a row belonged to
    // must survive being read the morning after.
    private readonly string _claimant = $"{Environment.MachineName}/{Guid.CreateVersion7():N}";

    /// <summary>
    /// Polls until the host shuts down: drain the table, sleep one interval, repeat.
    /// </summary>
    /// <param name="stoppingToken">Signaled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);

        try
        {
            do
            {
                await DrainAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown. A message caught mid-delivery is re-delivered once its lease lapses —
            // at-least-once means never having to finish on the way out.
        }
    }

    private async Task DrainAsync(CancellationToken stoppingToken)
    {
        try
        {
            int claimedCount;
            do
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                claimedCount = await processor.ProcessBatchAsync(_claimant, stoppingToken);
            }
            while (claimedCount > 0);

            // Once the table is quiet, trim the delivered history past its retention. In here
            // rather than a janitor of its own: the worker that writes the history is the right
            // owner of trimming it, and a failing sweep lands in the same catch below (ADR 0033).
            using (var scope = scopeFactory.CreateScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await processor.SweepDeliveredAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Draining the outbox failed; the next poll retries.");
        }
    }
}
