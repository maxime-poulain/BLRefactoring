using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// What an operator can see and do about the messages delivery gave up on (ADR 0061).
/// </summary>
/// <remarks>
/// Against SQLite through the real model, for the reason <c>TitleUniquenessTests</c> gives: the
/// selection is a predicate a database has to answer, and the in-memory provider would answer it in
/// LINQ to objects — the one evaluation this suite exists not to trust.
/// <para>
/// It does not exercise <c>OutboxProcessor</c>, and cannot: the worker's claim is an
/// <c>UPDATE … OUTPUT</c> with <c>READPAST</c>, which is SQL Server and nothing else. What the
/// requeue owes the processor — that the delivery ledger survives it — is asserted here on the rows,
/// and end to end by the Docker-bound suites.
/// </para>
/// </remarks>
public sealed class OutboxOperationsTests : IAsyncLifetime
{
    private const int MaxAttempts = 5;

    private static readonly DateTime Recorded = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A clock stopped at a value the assertions can name.</summary>
    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Requeued = new(2026, 8, 9, 18, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly TimeProvider _clock = new StoppedClock(Requeued);
    private TrainingContext _context = null!;
    private IOutboxOperations _operations = null!;

    /// <summary>Opens the connection the database lives inside, and builds the schema on it.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingContext(new DbContextOptionsBuilder<TrainingContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
            .Options);

        await _context.Database.EnsureCreatedAsync();

        _operations = new OutboxOperations(
            _context,
            _clock,
            Options.Create(new OutboxOptions { MaxAttempts = MaxAttempts }),
            NullLogger<OutboxOperations>.Instance);
    }

    /// <summary>Closes the context and, with the connection, the database itself.</summary>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Get poisoned async, answers the spent budgets, and nothing else.
    /// </summary>
    /// <remarks>
    /// The predicate is the claim query's exclusion read the other way round, so this fact is what
    /// keeps the two definitions of "poison" the same one.
    /// </remarks>
    [Fact]
    public async Task GetPoisonedAsync_AnswersTheSpentBudgets_AndNothingElse()
    {
        var poisoned = await GivenMessageAsync("TrainerCreated", Recorded, attempts: MaxAttempts);
        await GivenMessageAsync("TrainingCreated", Recorded, attempts: MaxAttempts - 1);
        await GivenMessageAsync("TrainingEdited", Recorded, attempts: 0);
        await GivenMessageAsync("TrainingDeleted", Recorded, attempts: MaxAttempts, delivered: true);

        var page = await _operations.GetPoisonedAsync(new PageRequest());

        page.TotalCount.Should().Be(1, "a message is poison only once its budget is spent and it is still owed");
        page.Items.Should().ContainSingle().Which.Id.Should().Be(poisoned.Id);
    }

    /// <summary>
    /// Get poisoned async, answers in the order the worker would have delivered them.
    /// </summary>
    [Fact]
    public async Task GetPoisonedAsync_AnswersInTheOrderTheWorkerWouldHaveDeliveredThem()
    {
        var newest = await GivenMessageAsync("TrainerCreated", Recorded.AddMinutes(2), attempts: MaxAttempts);
        var oldest = await GivenMessageAsync("TrainingCreated", Recorded, attempts: MaxAttempts);
        var middle = await GivenMessageAsync("TrainingEdited", Recorded.AddMinutes(1), attempts: MaxAttempts);

        var page = await _operations.GetPoisonedAsync(new PageRequest());

        page.Items.Select(message => message.Id).Should().Equal(
            [oldest.Id, middle.Id, newest.Id],
            "a backlog is read oldest first, which is the order it would have been delivered in");
    }

    /// <summary>
    /// Get poisoned async, counts everything poisoned, and answers one page of it.
    /// </summary>
    [Fact]
    public async Task GetPoisonedAsync_CountsEverythingPoisoned_AndAnswersOnePageOfIt()
    {
        for (var minute = 0; minute < 3; minute++)
        {
            await GivenMessageAsync("TrainerCreated", Recorded.AddMinutes(minute), attempts: MaxAttempts);
        }

        var page = await _operations.GetPoisonedAsync(new PageRequest { Page = 2, PageSize = 2 });

        page.TotalCount.Should().Be(3, "the count describes the whole backlog, not the slice read");
        page.Items.Should().ContainSingle();
    }

    /// <summary>
    /// Get poisoned async, names the consumers a retry would skip.
    /// </summary>
    /// <remarks>
    /// The column that turns "press retry and hope" into a decision: the ledger's rows say what a
    /// requeue will <em>not</em> run again (ADR 0034).
    /// </remarks>
    [Fact]
    public async Task GetPoisonedAsync_NamesTheConsumersARetryWouldSkip()
    {
        var message = await GivenMessageAsync("TrainerCreated", Recorded, attempts: MaxAttempts);
        var untouched = await GivenMessageAsync("TrainingCreated", Recorded.AddMinutes(1), attempts: MaxAttempts);
        await GivenDeliveryAsync(message, "WelcomeEmail");
        await GivenDeliveryAsync(message, "SearchIndex");

        var page = await _operations.GetPoisonedAsync(new PageRequest());

        page.Items.Single(entry => entry.Id == message.Id).DeliveredConsumers
            .Should().Equal(["SearchIndex", "WelcomeEmail"]);
        page.Items.Single(entry => entry.Id == untouched.Id).DeliveredConsumers
            .Should().BeEmpty("no consumer settled, so a retry runs all of them");
    }

    /// <summary>
    /// Requeue async, gives back the budget, and leaves the ledger alone.
    /// </summary>
    /// <remarks>
    /// The two halves of the operation, asserted together because separating them would let one
    /// pass while the other made it dangerous.
    /// </remarks>
    [Fact]
    public async Task RequeueAsync_GivesBackTheBudget_AndLeavesTheLedgerAlone()
    {
        var message = await GivenMessageAsync("TrainerCreated", Recorded, attempts: MaxAttempts);
        await GivenDeliveryAsync(message, "WelcomeEmail");

        var result = await _operations.RequeueAsync(message.Id);

        result.Match(() => true, _ => false).Should().BeTrue();

        var reloaded = await _context.Set<OutboxMessage>().SingleAsync(candidate => candidate.Id == message.Id);
        reloaded.Attempts.Should().Be(0);
        reloaded.NextAttemptOnUtc.Should().BeNull("an operator asking for a retry means now");
        reloaded.RequeuedOnUtc.Should().Be(_clock.GetUtcNow().UtcDateTime);
        reloaded.MayHaveSettledConsumers.Should().BeTrue(
            "the counter no longer answers that question, and the processor asks this one");

        var ledger = await _context.Set<OutboxMessageConsumer>()
            .Where(delivery => delivery.MessageId == message.Id)
            .ToListAsync();

        ledger.Should().ContainSingle().Which.ConsumerName.Should().Be(
            "WelcomeEmail", "a retry that replayed a settled consumer would send a second welcome email");
    }

    /// <summary>
    /// Requeue async, a message nobody stored, is refused as not found.
    /// </summary>
    [Fact]
    public async Task RequeueAsync_AMessageNobodyStored_IsRefusedAsNotFound()
    {
        var result = await _operations.RequeueAsync(Guid.CreateVersion7());

        ErrorCodesOf(result).Should().Equal([ErrorCodes.NotFound]);
    }

    /// <summary>
    /// Requeue async, a message still inside its budget, is refused as not poison.
    /// </summary>
    /// <remarks>
    /// It is already scheduled. Accepting this would move its next attempt forward, which is a
    /// decision nobody asked for and one no button on the screen offers.
    /// </remarks>
    [Fact]
    public async Task RequeueAsync_AMessageStillInsideItsBudget_IsRefusedAsNotPoison()
    {
        var message = await GivenMessageAsync("TrainerCreated", Recorded, attempts: MaxAttempts - 1);

        var result = await _operations.RequeueAsync(message.Id);

        ErrorCodesOf(result).Should().Equal([OutboxErrorCodes.NotPoison]);
    }

    /// <summary>
    /// Requeue async, a message already delivered, is refused as not poison.
    /// </summary>
    [Fact]
    public async Task RequeueAsync_AMessageAlreadyDelivered_IsRefusedAsNotPoison()
    {
        var message = await GivenMessageAsync(
            "TrainerCreated", Recorded, attempts: MaxAttempts, delivered: true);

        var result = await _operations.RequeueAsync(message.Id);

        ErrorCodesOf(result).Should().Equal([OutboxErrorCodes.NotPoison]);
    }

    private static IReadOnlyList<ErrorCode> ErrorCodesOf(Shared.Common.Results.Result result) =>
        result.Match(
            () => [],
            errors => (IReadOnlyList<ErrorCode>)[.. errors.Select(error => error.ErrorCode)]);

    private async Task<OutboxMessage> GivenMessageAsync(
        string name,
        DateTime occurredOnUtc,
        int attempts,
        bool delivered = false)
    {
        var message = new OutboxMessage(Guid.CreateVersion7(), name, 1, "{}", occurredOnUtc);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            message.RecordFailure("nobody could read it", occurredOnUtc, TimeSpan.FromSeconds(30));
        }

        if (delivered)
        {
            message.MarkProcessed(occurredOnUtc.AddMinutes(1));
        }

        _context.Set<OutboxMessage>().Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    private async Task GivenDeliveryAsync(OutboxMessage message, string consumerName)
    {
        _context.Set<OutboxMessageConsumer>().Add(
            new OutboxMessageConsumer(message.Id, consumerName, Recorded.AddMinutes(1)));

        await _context.SaveChangesAsync();
    }
}
