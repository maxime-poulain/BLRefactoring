using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Search;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Search;

/// <summary>
/// A database the search index actually lives in, for the two suites that read it back.
/// </summary>
/// <remarks>
/// Against a database rather than a substitute, because everything worth asserting here is a
/// translation: the cascade that takes a training's tokens with it, the single statement that flips
/// a whole catalog, and the composed <c>EXISTS</c> per word. A fake indexer would agree with any
/// of those and prove none of them.
/// <para>
/// SQLite over the in-memory provider for the reason <c>TrainerNamesQueryTests</c> gives: the
/// in-memory provider answers in LINQ to objects and cannot query a complex property at all
/// (ADR 0032). The schema comes from the model rather than from the migrations, which are scaffolded
/// for SQL Server.
/// </para>
/// </remarks>
public abstract class SearchIndexTest : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>The session every fact in these suites reads and writes through.</summary>
    protected TrainingContext Context { get; private set; } = null!;

    /// <summary>The adapter under test, or the one that fills the index for the reader's facts.</summary>
    protected TrainingSearchIndexer Indexer { get; private set; } = null!;

    /// <summary>Opens the connection the database lives inside, and builds the schema on it.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        Context = new TrainingContext(new DbContextOptionsBuilder<TrainingContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
            // The production interceptor, on a clock that moves a full second per reading: every
            // training carries its own CreatedOn, so the facts about the "newest" order (ADR 0071)
            // order real instants rather than a table of year-one defaults.
            .AddInterceptors(new AuditableEntitiesInterceptor(new SteppingClock()))
            .Options);

        await Context.Database.EnsureCreatedAsync();

        Indexer = new TrainingSearchIndexer(Context);
    }

    /// <summary>Closes the context and, with the connection, the database itself.</summary>
    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Adds a trainer, in good standing unless asked otherwise.</summary>
    protected async Task<Trainer> GivenTrainerAsync(bool suspended = false)
    {
        var trainer = new TrainerBuilder()
            .WithFirstname("Ada")
            .WithLastname("Lovelace")
            .WithContactEmail($"ada.{Guid.NewGuid():N}@example.com")
            .Build();

        if (suspended)
        {
            trainer.Suspend(SuspensionReason.Create("Repeated policy breaches.").ShouldBeSuccess());
        }

        Context.Trainers.Add(trainer);
        await Context.SaveChangesAsync();

        return trainer;
    }

    /// <summary>Adds a training under that trainer, on offer unless asked otherwise.</summary>
    /// <remarks>
    /// A training is born published, which the aggregate states in its initializer, so the
    /// interesting direction is the other one: withdrawing it is what this helper has to do work for.
    /// </remarks>
    protected async Task<Training> GivenTrainingAsync(
        Trainer owner,
        string title,
        bool published = true,
        string[]? topics = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var builder = new TrainingBuilder()
            .WithTitle(title)
            .WithTrainerId(owner.Id.Value);

        if (topics is not null)
        {
            builder = builder.WithTopics(topics);
        }

        var training = (await builder.BuildAsync()).ShouldBeSuccess();

        if (!published)
        {
            training.Unpublish().ShouldBeSuccess();
        }

        Context.Trainings.Add(training);
        await Context.SaveChangesAsync();

        return training;
    }

    /// <summary>
    /// Reads one entry back, tokens and topics included, without the tracker answering from memory.
    /// </summary>
    protected async Task<TrainingSearchEntry?> EntryOfAsync(Guid trainingId)
    {
        Context.ChangeTracker.Clear();

        return await Context.Set<TrainingSearchEntry>()
            .Include(entry => entry.Terms)
            .Include(entry => entry.Topics)
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.TrainingId == trainingId);
    }

    /// <summary>
    /// A clock that moves a full second per reading, so what is saved first is older — by enough
    /// that no rounding anywhere can make two instants one.
    /// </summary>
    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now = _now.AddSeconds(1);
    }
}
