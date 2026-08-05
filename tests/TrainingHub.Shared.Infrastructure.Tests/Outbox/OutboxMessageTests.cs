using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The envelope's two state transitions: delivered, and failed-with-a-schedule.
/// </summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 30, 0, DateTimeKind.Utc);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

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
    /// Mark processed, after a failure, clears the schedule.
    /// </summary>
    [Fact]
    public void MarkProcessed_AfterAFailure_ClearsTheSchedule()
    {
        var message = CreateMessage();
        message.RecordFailure("a passing cloud", Now, RetryDelay);

        message.MarkProcessed(Now.AddMinutes(1));

        message.NextAttemptOnUtc.Should().BeNull("a delivered message schedules nothing");
    }

    /// <summary>
    /// Record failure, spends one attempt, keeps the reason, and books the next try.
    /// </summary>
    [Fact]
    public void RecordFailure_SpendsOneAttempt_KeepsTheReason_AndBooksTheNextTry()
    {
        var message = CreateMessage();

        message.RecordFailure("first reason", Now, RetryDelay);

        message.Attempts.Should().Be(1, "every failed delivery spends exactly one attempt");
        message.Error.Should().Be("first reason");
        message.ProcessedOnUtc.Should().BeNull();
        message.ClaimedUntil.Should().BeNull("releasing the lease is what lets any worker retry");
        message.NextAttemptOnUtc.Should().Be(
            Now + RetryDelay, "the first failure waits one base delay before the next try");
    }

    /// <summary>
    /// Record failure, again, doubles the schedule.
    /// </summary>
    [Fact]
    public void RecordFailure_Again_DoublesTheSchedule()
    {
        var message = CreateMessage();
        var later = Now.AddSeconds(30);

        message.RecordFailure("first reason", Now, RetryDelay);
        message.RecordFailure("second reason", later, RetryDelay);

        message.Attempts.Should().Be(2);
        message.Error.Should().Be("second reason", "only the latest failure is worth an operator's time");
        message.NextAttemptOnUtc.Should().Be(
            later + (RetryDelay * 2), "the schedule doubles with each spent attempt");
    }
}
