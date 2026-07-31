using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// Publishes committed integration events, oldest first, in bounded batches.
/// </summary>
/// <remarks>
/// <para>
/// Polling rather than reacting: the point of the outbox is to survive the process dying between
/// the commit and the publication, and only a query against the table can recover from that. An
/// in-memory signal would be lost with the process that held it.
/// </para>
/// <para>
/// Delivery is at-least-once. A message published just before a crash — before its
/// <c>ProcessedOn</c> is written — is published again on restart, so handlers must tolerate a
/// repeat. Making it exactly-once would need the handler's effect and the marking to share a
/// transaction, which is impossible once the effect leaves the database.
/// </para>
/// </remarks>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failure here is the loop's own — a broken connection, say. Letting it escape
                // would stop the service for the lifetime of the process, so the batch is
                // abandoned and the next tick tries again.
                logger.LogError(exception, "Outbox batch failed. Retrying at the next interval.");
            }

            try
            {
                await Task.Delay(PollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Publishes one batch of pending messages.
    /// </summary>
    /// <remarks>
    /// Its own scope, and its own <c>DbContext</c>: this service outlives every request, so
    /// resolving a scoped context from the root provider would pin one for the life of the
    /// process — accumulating tracked entities and handing every batch a stale change tracker.
    /// <para>
    /// Public rather than private-with-a-test-hatch: draining the queue on demand is a real
    /// operation, whether that is a test wanting a deterministic result instead of a ten-second
    /// wait, or an operator flushing the backlog after fixing whatever was rejecting messages.
    /// </para>
    /// </remarks>
    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();

        var pending = await context.Set<OutboxMessage>()
            .Where(message => message.ProcessedOn == null)
            .OrderBy(message => message.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var message in pending)
        {
            message.AttemptCount++;

            try
            {
                var integrationEvent = OutboxSerializer.Deserialize(message.Type, message.Payload);
                await dispatcher.DispatchAsync(integrationEvent, cancellationToken);

                message.ProcessedOn = timeProvider.GetUtcNow();
                message.Error = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Recorded on the row rather than only in the log, so a stuck message can be
                // diagnosed from the table. It stays pending and will be picked up again.
                message.Error = exception.Message;

                logger.LogError(
                    exception,
                    "Publishing outbox message {MessageId} of type {MessageType} failed on attempt {Attempt}.",
                    message.Id,
                    message.Type,
                    message.AttemptCount);
            }
        }

        // One save for the batch. Attempt counts and errors are persisted alongside the
        // successes, so a message that keeps failing is visible rather than silently retried.
        await context.SaveChangesAsync(cancellationToken);
    }
}
