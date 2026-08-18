using System.Globalization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Development;
using TrainingHub.Shared.Infrastructure.Extensions;
using TrainingHub.Shared.Infrastructure.Photos;
using TrainingHub.Shared.Infrastructure.Repositories;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using TrainingHub.Shared.Storage;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Extensions;

/// <summary>
/// The seeder that fills a development database, run against one (ADR 0079).
/// </summary>
/// <remarks>
/// The corpus and the dataset are pure, and <c>DevelopmentCatalogTests</c> checks every word of them
/// without a container anywhere. This is the other half: the two hundred statements standing between
/// that dataset and a database, which nothing had ever executed. What they do is not obvious from
/// reading them — three gates, a transaction per person, an aggregate behind every row, and one
/// deliberate write around the audit interceptor.
/// <para>
/// Against SQLite in memory rather than doubles, in the mold of
/// <c>AdministratorSeedExtensionsTests</c>: a real <see cref="UserManager{TUser}"/> under the
/// product's own password policy, the real repositories, the real sanitizer, the real audit
/// interceptor, and a real <see cref="IHost"/> whose scopes the seeder opens for itself. A mock would
/// replay the calls this code already makes; what is worth asserting is what the database ends up
/// holding.
/// </para>
/// <para>
/// The schema comes from the model through <c>EnsureCreated</c> rather than from the migrations —
/// those are scaffolded for SQL Server, and building them here would prove something else.
/// </para>
/// </remarks>
public sealed class DevelopmentSeedExtensionsTests
{
    /// <summary>
    /// Small enough to run in a moment, large enough that several trainers own several trainings.
    /// </summary>
    private const int Trainings = 40;

    /// <summary>
    /// What this world's configuration offers the seeder, and the word the repository ships.
    /// </summary>
    /// <remarks>
    /// The same one <c>appsettings.Development.json</c> carries, rather than an invented fixture:
    /// this is what would notice the day <c>toto</c> stopped satisfying the configured policy and
    /// the seeder started creating nobody. <c>TheSeededPassword_SatisfiesTheProductionPolicy</c>
    /// asks the same question of the shipped file itself.
    /// </remarks>
    private const string ConfiguredPassword = "toto";

    /// <summary>The instant this world's clock answers, and the anchor the dataset is placed around.</summary>
    private static readonly DateTime Anchor = new(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// During client generation, it seeds nothing.
    /// </summary>
    /// <remarks>
    /// The same guard the administrator's seeder carries, for the same reason: emitting the OpenAPI
    /// document loads a host for real, so without this, generating a client would write a catalog
    /// into whatever database a developer's configuration happens to name.
    /// <para>
    /// The variable is set and put back inline rather than through a helper. It is process-wide, so
    /// leaving it behind would change the answer for everything that ran afterwards; this assembly
    /// holds no other class that reads it, and xUnit runs the methods of one class in sequence, so
    /// the interleaving a collection would guard against cannot happen here.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task DuringClientGeneration_ItSeedsNothing()
    {
        await using var world = await World.CreateAsync();

        var previous = Environment.GetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable);
        Environment.SetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, "1");

        try
        {
            await world.Host.EnsureDevelopmentCatalogAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, previous);
        }

        (await world.TrainersAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Outside development, it seeds nothing.
    /// </summary>
    /// <remarks>
    /// Everything else is in place — the key says yes, the count is set — and still nothing happens.
    /// Every account this seeder writes shares one password committed in plain sight, so this gate is
    /// the whole of what keeps a development convenience from being a catalog of known credentials.
    /// </remarks>
    [Fact]
    public async Task OutsideDevelopment_ItSeedsNothing()
    {
        await using var world = await World.CreateAsync(environment: Environments.Production);

        await world.Host.EnsureDevelopmentCatalogAsync();

        (await world.TrainersAsync()).Should()
            .BeEmpty("a catalog of accounts sharing one committed password is what this gate refuses");
    }

    /// <summary>
    /// With the key unset, it seeds nothing.
    /// </summary>
    /// <remarks>
    /// The state of every machine that has not asked for the data, this repository's CI included. A
    /// quiet no-op rather than a failure, because the third gate exists precisely so that somebody
    /// who only wanted a host to start does not discover five hundred trainings.
    /// </remarks>
    [Fact]
    public async Task WithTheKeyUnset_ItSeedsNothing()
    {
        await using var world = await World.CreateAsync(enabled: false);

        await world.Host.EnsureDevelopmentCatalogAsync();

        (await world.TrainersAsync()).Should().BeEmpty();
        (await world.TrainingsAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// With no password configured, it reports rather than invents.
    /// </summary>
    /// <remarks>
    /// The branch the key brought with it, and the same answer the administrator's seeder gives to
    /// its own missing key: a credential nobody wrote down is not one to put into a database, and a
    /// host that refused to start over a development convenience would be worse than one that says
    /// which key is missing. Asserted because the failure is a log line rather than an exception, so
    /// the version that read a null and handed it to Identity would create a hundred and seventy
    /// nothing-happened warnings instead of one sentence naming the cause.
    /// </remarks>
    [Fact]
    public async Task WithNoPasswordConfigured_ItReportsRatherThanInvents()
    {
        await using var world = await World.CreateAsync(password: null);

        var seeding = () => world.Host.EnsureDevelopmentCatalogAsync();

        await seeding.Should().NotThrowAsync();
        (await world.TrainersAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// The number asked for, is the number created.
    /// </summary>
    /// <remarks>
    /// Every training goes through <c>Training.CreateAsync</c> against the same three ports the
    /// application layer resolves, and any of them could refuse one — the seeder logs it and carries
    /// on, by design. An exact count is what tells a dataset the domain accepts whole from one it
    /// silently trims.
    /// </remarks>
    [Fact]
    public async Task TheNumberAskedFor_IsTheNumberCreated()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        (await world.TrainingsAsync()).Should().HaveCount(Trainings);
        (await world.TrainersAsync()).Should().HaveCount(Dataset.Count, "every trainer the dataset places is created");
    }

    /// <summary>
    /// Every seeded trainer, owns between one and five trainings.
    /// </summary>
    /// <remarks>
    /// The lower bound is what makes the catalog usable — a trainer with nothing is a profile page
    /// that proves nothing — and the upper one is deliberately below the domain's limit of ten, so a
    /// seeded catalog sitting on the quota does not refuse the first training somebody creates by
    /// hand and make the rule look like a defect.
    /// </remarks>
    [Fact]
    public async Task EverySeededTrainer_OwnsBetweenOneAndFiveTrainings()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var owned = (await world.TrainingsAsync())
            .GroupBy(training => training.TrainerId)
            .Select(owner => owner.Count())
            .ToList();

        owned.Should().HaveCount(Dataset.Count, "nobody is left without a catalog");
        owned.Should().AllSatisfy(count => count.Should().BeInRange(
            DevelopmentDataset.MinimumTrainingsPerTrainer,
            DevelopmentDataset.MaximumTrainingsPerTrainer));
    }

    /// <summary>
    /// A seeded account, signs in with the committed password.
    /// </summary>
    /// <remarks>
    /// The promise this seeder makes in its own log line, and the first one a developer acts on.
    /// Asserted through <see cref="UserManager{TUser}.CheckPasswordAsync"/> against the production
    /// hasher, so it is the credential that is proven rather than a string being passed along: the
    /// day <c>toto</c> stops satisfying the configured policy (ADR 0051), Identity refuses it, the
    /// seeder logs a warning and creates nobody, and this is what notices.
    /// </remarks>
    [Fact]
    public async Task ASeededAccount_SignsInWithTheCommittedPassword()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var username = Dataset[0].Username;

        (await world.CanSignInAsync(username, ConfiguredPassword)).Should().BeTrue();
        (await world.CanSignInAsync(username, "not-the-committed-one")).Should().BeFalse();
    }

    /// <summary>
    /// Every training, carries the instant the dataset scheduled it at.
    /// </summary>
    /// <remarks>
    /// The one write this seeder makes outside an aggregate, and the only assertion that can tell it
    /// happened. <c>CreatedOn</c> is stamped by the audit interceptor from the clock on insert, and
    /// that interceptor is registered here exactly as a host registers it — so without the bulk
    /// update every training would carry the single instant this world's clock answers, and the
    /// newest-first page would be decided entirely by the identifier tiebreaker (ADR 0071).
    /// </remarks>
    [Fact]
    public async Task EveryTraining_CarriesTheInstantTheDatasetScheduledItAt()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var scheduled = Dataset
            .SelectMany(trainer => trainer.Trainings)
            .Select(training => training.CreatedOnUtc)
            .Order()
            .ToList();

        var stamped = (await world.TrainingsAsync())
            .Select(training => training.CreatedOn)
            .Order()
            .ToList();

        stamped.Should().Equal(scheduled, "the bulk update is what puts a seeded catalog in the past");
        stamped.Should().NotContain(Anchor, "a merely stamped row would carry this world's clock instead");
    }

    /// <summary>
    /// The minority states, are all present.
    /// </summary>
    /// <remarks>
    /// A catalog where everything is published exercises one branch of every screen. Each of these
    /// three is a different path through the seeder: an aggregate method before the save, another one
    /// before it, and a second <c>SaveChangesAsync</c> for the suspension alone — which happens last
    /// on purpose, because a suspended trainer may publish nothing (ADR 0050).
    /// </remarks>
    [Fact]
    public async Task TheMinorityStates_AreAllPresent()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var trainings = await world.TrainingsAsync();

        trainings.Should().Contain(
            training => training.Status == TrainingStatus.Unpublished,
            "a training its owner took down");
        trainings.Should().Contain(
            training => training.Status == TrainingStatus.Withheld && training.WithholdingReason != null,
            "a training the administration withheld, with the reason beside it");
        (await world.TrainersAsync()).Should().Contain(
            trainer => trainer.SuspensionReason != null,
            "a suspended trainer, suspended after their catalog existed");
    }

    /// <summary>
    /// Every portrait, reaches the store and the aggregate.
    /// </summary>
    /// <remarks>
    /// The drawn bytes go through the same three steps as a real upload and in the same order (ADR
    /// 0063) — vetted, stripped, then described from what is about to be stored — and any of them
    /// could refuse. Two counts rather than one: the store proves the bytes left, and the persisted
    /// aggregate proves <c>AttachPhoto</c> ran and was saved. A seeder that stored the bytes and
    /// forgot the aggregate would leave every profile faceless and every object in the bucket
    /// orphaned.
    /// </remarks>
    [Fact]
    public async Task EveryPortrait_ReachesTheStoreAndTheAggregate()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var faces = Dataset.Count(trainer => trainer.PortraitHue is not null);

        faces.Should().BeGreaterThan(0, "the dataset gives most of its trainers a face");
        world.Portraits.Should().HaveCount(faces);
        world.Portraits.Should().AllSatisfy(content => content.Length.Should().BeGreaterThan(0));
        (await world.TrainersAsync()).Count(trainer => trainer.Photo is not null).Should().Be(faces);
    }

    /// <summary>
    /// A second run, creates nothing.
    /// </summary>
    /// <remarks>
    /// The idempotence, and the whole of it: the dataset derives its usernames from the people it
    /// invents, so a second run asks the same question and skips every one of them. Without it a
    /// developer who starts a host twice gets twice the catalog — and would not, because the second
    /// run would fail on the uniqueness of the first account instead.
    /// </remarks>
    [Fact]
    public async Task ASecondRun_CreatesNothing()
    {
        await using var world = await World.CreateAsync();

        await world.Host.EnsureDevelopmentCatalogAsync();

        var trainers = (await world.TrainersAsync()).Count;
        var trainings = (await world.TrainingsAsync()).Count;

        await world.Host.EnsureDevelopmentCatalogAsync();

        (await world.TrainersAsync()).Should().HaveCount(trainers);
        (await world.TrainingsAsync()).Should().HaveCount(trainings);
    }

    /// <summary>
    /// What the seeder is expected to place, computed the way the seeder computes it.
    /// </summary>
    /// <remarks>
    /// The dataset is pure and deterministic, so asking it the same question with the same arguments
    /// answers the same people. That is what lets a fact name a username or count the faces without
    /// a second copy of the corpus living in this file.
    /// </remarks>
    private static IReadOnlyList<SeededTrainer> Dataset { get; } = DevelopmentDataset.Build(Trainings, Anchor);

    /// <summary>
    /// A host, two contexts over one connection, and the identity store the seeder writes into.
    /// </summary>
    /// <remarks>
    /// <b>One connection.</b> Both contexts are built over the same open
    /// <see cref="SqliteConnection"/>, which is what keeps the database alive at all: SQLite discards
    /// a <c>:memory:</c> store when the last connection to it closes.
    /// <para>
    /// <b>The transaction is the one thing this world cannot hold, and saying so is the point.</b>
    /// The seeder wraps each trainer in a <see cref="System.Transactions.TransactionScope"/>
    /// (ADR 0040), and the SQLite provider implements no ambient transactions whatsoever — it
    /// refuses the first save rather than enlisting. The warning is suppressed so that the statements
    /// run, which means every fact below is about a seeder whose writes were <em>not</em> atomic.
    /// Nothing here asserts that they are: the interrupted-run behavior the record claims belongs to
    /// SQL Server, and the integration suites are where a database that implements it runs. What this
    /// world proves is everything else — the gates, the counts, the credential, the states and the
    /// backdating — and it would be worse to prove none of it than to prove it without the
    /// transaction.
    /// </para>
    /// <para>
    /// <b>The second context cannot use <c>EnsureCreated</c>.</b> It would find a database that
    /// already exists with tables in it — the first context created them — and return having created
    /// nothing. Asking the creator for its tables directly is the operation that means what is wanted
    /// here, and getting it wrong fails as a missing <c>AspNetUsers</c> at the first account.
    /// </para>
    /// <para>
    /// <b>What is substituted, and only this.</b> The object store is S3 and the dispatcher's
    /// destination is MediatR and fourteen consumers, so a recording store stands in for the first
    /// and a no-op for the second. Both interceptors are the real ones and both run: the audit stamps
    /// are what two of these facts are about, and the domain-event interceptor collects and clears
    /// exactly as it does in a host. What is given up is the outbox rows a real run writes — those
    /// have their own suite, and asserting them here would be testing the pipeline rather than the
    /// seeder.
    /// </para>
    /// </remarks>
    private sealed class World : IAsyncDisposable
    {
        private World(IHost host, SqliteConnection connection, RecordingPhotoStore portraits)
        {
            Host = host;
            _connection = connection;
            _portraits = portraits;
        }

        /// <summary>The host the seeder is an extension of.</summary>
        public IHost Host { get; }

        private readonly SqliteConnection _connection;

        private readonly RecordingPhotoStore _portraits;

        /// <summary>Every portrait the seeder handed the store.</summary>
        public IReadOnlyList<ReadOnlyMemory<byte>> Portraits => _portraits.Stored;

        /// <param name="enabled">Whether the third gate's key says yes.</param>
        /// <param name="password">What the configuration offers to create the accounts with, or
        /// <see langword="null"/> for a machine whose file names none.</param>
        /// <param name="environment">Defaulted through a null rather than in the signature:
        /// <c>Environments.Development</c> is a static field rather than a constant.</param>
        public static async Task<World> CreateAsync(
            bool enabled = true, string? environment = null, string? password = ConfiguredPassword)
        {
            environment ??= Environments.Development;

            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var portraits = new RecordingPhotoStore();

            var host = new HostBuilder()
                .UseEnvironment(environment)
                .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [DevelopmentSeedExtensions.EnabledConfigurationKey] =
                            enabled.ToString(CultureInfo.InvariantCulture),
                        [DevelopmentSeedExtensions.TrainingsConfigurationKey] =
                            Trainings.ToString(CultureInfo.InvariantCulture),
                        [DevelopmentSeedExtensions.PasswordConfigurationKey] = password
                    }))
                .ConfigureServices(services => Register(services, connection, portraits))
                .Build();

            using (var scope = host.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<TrainingContext>()
                    .Database.EnsureCreatedAsync();

                await scope.ServiceProvider.GetRequiredService<TrainingIdentityDbContext>()
                    .Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            }

            return new World(host, connection, portraits);
        }

        /// <summary>Everything a host gives the seeder, over this world's one connection.</summary>
        private static void Register(
            IServiceCollection services, SqliteConnection connection, ITrainerPhotoStore portraits)
        {
            services.AddLogging();

            // Fixed, so that a merely stamped row is recognizable: it would carry this instant, and
            // a backdated one carries the dataset's.
            services.AddSingleton<TimeProvider>(new FrozenClock(Anchor));

            services.AddSingleton<AuditableEntitiesInterceptor>();
            services.AddScoped<IDomainEventDispatcher, NoDispatch>();
            services.AddScoped<DomainEventInterceptor>();

            services.AddDbContext<TrainingContext>((serviceProvider, options) => options
                .UseSqlite(connection)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning))
                .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
                .AddInterceptors(
                    serviceProvider.GetRequiredService<DomainEventInterceptor>(),
                    serviceProvider.GetRequiredService<AuditableEntitiesInterceptor>()));

            services.AddDbContext<TrainingIdentityDbContext>(options => options
                .UseSqlite(connection)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning)));

            services
                // The product's own password rules, mirrored from AddApiIdentity. Left at Identity's
                // defaults this world would refuse `toto`, the seeder would create nobody, and every
                // count here would be zero for a reason having nothing to do with the seeder.
                .AddIdentityCore<IdentityUser<Guid>>(options =>
                {
                    options.Password.RequireUppercase = false;
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 4;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddEntityFrameworkStores<TrainingIdentityDbContext>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITrainerRepository, TrainerRepository>();
            services.AddScoped<ITrainingRepository, TrainingRepository>();

            // Resolved through the repository rather than registered a second time against the same
            // implementation, exactly as AddInfrastructure does it.
            services.AddScoped<IUniquenessTitleChecker>(serviceProvider =>
                (TrainingRepository)serviceProvider.GetRequiredService<ITrainingRepository>());
            services.AddScoped<ITrainingCounter>(serviceProvider =>
                (TrainingRepository)serviceProvider.GetRequiredService<ITrainingRepository>());
            services.AddScoped<ITrainerStanding>(serviceProvider =>
                (TrainerRepository)serviceProvider.GetRequiredService<ITrainerRepository>());

            // The real adapter over both stores, exactly as AddInfrastructure registers it: the
            // seeder confirms every account it creates (ADR 0090), and whether that satisfies the
            // factory's verification check is one of the things this world must prove.
            services.AddScoped<ITrainerVerification, TrainerVerification>();

            // The real decoder: whether a drawn portrait survives the rules is one of the things this
            // seeder is asserted to get right.
            services.AddSingleton<IPhotoSanitizer, SkiaSharpPhotoSanitizer>();
            services.AddSingleton(portraits);
        }

        /// <summary>Every trainer the database holds.</summary>
        public Task<List<Trainer>> TrainersAsync() => ReadAsync(context => context.Trainers.ToListAsync());

        /// <summary>Every training the database holds.</summary>
        public Task<List<Training>> TrainingsAsync() => ReadAsync(context => context.Trainings.ToListAsync());

        /// <summary>
        /// Whether an account holds that password.
        /// </summary>
        /// <remarks>
        /// The hash rather than a sign-in, for the reason the administrator's suite gives:
        /// <c>SignInManager</c> would drag lockout and cookie machinery into a question that is only
        /// about a credential.
        /// </remarks>
        public async Task<bool> CanSignInAsync(string username, string password)
        {
            using var scope = Host.Services.CreateScope();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

            var user = await users.FindByNameAsync(username);

            return user is not null && await users.CheckPasswordAsync(user, password);
        }

        /// <remarks>
        /// A fresh scope each time, as a later request would be: reading from the change tracker of
        /// the scope that wrote would answer from memory what the database is supposed to answer.
        /// </remarks>
        private async Task<TAnswer> ReadAsync<TAnswer>(Func<TrainingContext, Task<TAnswer>> question)
        {
            using var scope = Host.Services.CreateScope();

            return await question(scope.ServiceProvider.GetRequiredService<TrainingContext>());
        }

        public async ValueTask DisposeAsync()
        {
            Host.Dispose();
            await _connection.DisposeAsync();
        }
    }

    /// <summary>A clock answering one instant, so that a stamped row is recognizable.</summary>
    private sealed class FrozenClock(DateTime instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(instant, TimeSpan.Zero);
    }

    /// <summary>
    /// The dispatcher's destination, stood down.
    /// </summary>
    /// <remarks>
    /// The interceptor above it is the real one and still runs — it collects the events off the
    /// aggregates and clears them, which is what a host does. What this replaces is MediatR and the
    /// consumers behind it, whose work is to write outbox rows that have a suite of their own.
    /// </remarks>
    private sealed class NoDispatch : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// The object store, replaced by something that remembers.
    /// </summary>
    /// <remarks>
    /// S3 is the real destination, and standing it up here would prove something about SeaweedFS. The
    /// bytes are what is worth keeping: they are the evidence that a portrait left the seeder at all.
    /// </remarks>
    private sealed class RecordingPhotoStore : ITrainerPhotoStore
    {
        private readonly List<ReadOnlyMemory<byte>> _stored = [];

        public IReadOnlyList<ReadOnlyMemory<byte>> Stored => _stored;

        public Task StoreAsync(
            TrainerId trainerId,
            TrainerPhoto photo,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            _stored.Add(content);

            return Task.CompletedTask;
        }

        public Task<StoredObject?> FetchAsync(
            TrainerId trainerId, PhotoId photoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredObject?>(null);

        public Task DeleteAsync(
            TrainerId trainerId, PhotoId photoId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
