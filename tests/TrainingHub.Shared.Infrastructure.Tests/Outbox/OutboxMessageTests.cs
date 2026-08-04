using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The envelope's two state transitions: delivered, and failed-with-a-reason.
/// </summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 30, 0, DateTimeKind.Utc);

    private static OutboxMessage CreateMessage() =>
        new(Guid.CreateVersion7(), "TrainerCreated", 1, "{}", Now);

    /// <summary>
    /// Mark processed, stamps the delivery, and lets the lease go.
    /// </summary>
    [Fact]
    public void MarkProcessed_StampsTheDelivery_AndLetsTheLeaseGo()
    {
        var message = CreateMessage();

        message.MarkProcessed(Now.AddSeconds(1));

        message.ProcessedOnUtc.Should().Be(Now.AddSeconds(1));
        message.ClaimedUntil.Should().BeNull("a delivered message needs no lease to protect it");
    }

    /// <summary>
    /// Record failure, spends one attempt, keeps the reason, and returns the row to the pool.
    /// </summary>
    [Fact]
    public void RecordFailure_SpendsOneAttempt_KeepsTheReason_AndReturnsTheRowToThePool()
    {
        var message = CreateMessage();

        message.RecordFailure("first reason");
        message.RecordFailure("second reason");

        message.Attempts.Should().Be(2, "every failed delivery spends exactly one attempt");
        message.Error.Should().Be("second reason", "only the latest failure is worth an operator's time");
        message.ProcessedOnUtc.Should().BeNull();
        message.ClaimedUntil.Should().BeNull("releasing the lease is what lets the next poll retry");
    }
}
