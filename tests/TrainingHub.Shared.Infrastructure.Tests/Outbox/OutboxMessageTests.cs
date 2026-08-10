using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The envelope's three state transitions — delivered, failed-with-a-schedule, requeued — and the
/// question the processor asks it before reading the delivery ledger.
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

    /// <summary>
    /// Requeue, gives back the budget, the schedule, and keeps the reason it was retried for.
    /// </summary>
    [Fact]
    public void Requeue_GivesBackTheBudget_AndKeepsTheReasonItWasRetriedFor()
    {
        var message = CreateMessage();
        message.RecordFailure("the reason an operator read", Now, RetryDelay);
        message.RecordFailure("the reason an operator read", Now.AddMinutes(1), RetryDelay);

        message.Requeue(Now.AddHours(1));

        message.Attempts.Should().Be(0, "a requeue is a fresh budget, not one more attempt");
        message.NextAttemptOnUtc.Should().BeNull("an operator asking for a retry means now");
        message.ClaimedUntil.Should().BeNull();
        message.RequeuedOnUtc.Should().Be(Now.AddHours(1));
        message.Error.Should().Be(
            "the reason an operator read", "the evidence outlives the decision taken because of it");
    }

    /// <summary>
    /// May have settled consumers, is false for a message nothing has tried.
    /// </summary>
    [Fact]
    public void MayHaveSettledConsumers_IsFalse_ForAMessageNothingHasTried()
    {
        var message = CreateMessage();

        message.MayHaveSettledConsumers.Should().BeFalse(
            "no attempt has run a consumer, so the ledger cannot name one");
    }

    /// <summary>
    /// May have settled consumers, survives the requeue that resets the counter.
    /// </summary>
    /// <remarks>
    /// The fact this whole property exists for. The processor used to read <c>Attempts == 0</c>
    /// directly, which was exact for as long as nothing could put the counter back — and a requeue
    /// is exactly that. Answering <see langword="false"/> here makes the processor skip the ledger
    /// and re-run consumers that already succeeded: a second welcome email for one operator's
    /// retry, which is the guarantee ADR 0034 exists to give (ADR 0061).
    /// </remarks>
    [Fact]
    public void MayHaveSettledConsumers_SurvivesTheRequeue_ThatResetsTheCounter()
    {
        var message = CreateMessage();
        message.RecordFailure("a consumer that failed after its neighbor succeeded", Now, RetryDelay);

        message.Requeue(Now.AddHours(1));

        message.Attempts.Should().Be(0);
        message.MayHaveSettledConsumers.Should().BeTrue(
            "the ledger keeps its rows across a requeue, so the question the counter answered is no " +
            "longer one the counter can answer");
    }
}
