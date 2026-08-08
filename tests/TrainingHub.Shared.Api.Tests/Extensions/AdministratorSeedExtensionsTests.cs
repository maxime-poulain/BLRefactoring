using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Extensions;

/// <summary>
/// The only path by which anybody becomes an administrator (ADR 0051).
/// </summary>
/// <remarks>
/// There is deliberately no endpoint that grants a role, so this start-up seeder is the whole of the
/// grant on a development machine, and a database operation is the whole of it everywhere else. Two
/// of its branches are worth more than the rest: the environment gate, because without it a
/// convenience becomes a hole in production, and the client-generation guard, because emitting the
/// OpenAPI document loads a host for real against whatever database the configuration names.
/// <para>
/// Against a real <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> over SQLite
/// in memory rather than doubles. What is under test is what the managers end up holding, and a mock
/// would only replay the calls this method already makes. The schema comes from the model through
/// <c>EnsureCreated</c>, not from the migrations — those are scaffolded for SQL Server, and building
/// them here would prove something else.
/// </para>
/// </remarks>
[Collection(EnvironmentVariableCollection.Name)]
public sealed class AdministratorSeedExtensionsTests
{
    private const string Username = "ada.lovelace";

    /// <summary>
    /// During client generation, it grants nothing.
    /// </summary>
    /// <remarks>
    /// The same guard <c>EnsureDatabasesAreUpToDateAsync</c> carries, for the same reason: the
    /// document tool runs every statement above <c>app.Run()</c>, so without this, generating a
    /// client would write a role into a developer's real database.
    /// </remarks>
    [Fact]
    public async Task DuringClientGeneration_ItGrantsNothing()
    {
        // No account and a password to create one with, so a guard that failed would leave a
        // visible trace rather than merely skipping a grant.
        await using var world = await World.CreateAsync(
            username: Username, withAccount: false, password: "a-password");

        using (new TemporaryEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, "1"))
        {
            await world.Host.EnsureAdministratorAsync();
        }

        (await world.ExistsAsync(Username)).Should().BeFalse();
    }

    /// <summary>
    /// Outside development, it grants nothing.
    /// </summary>
    /// <remarks>
    /// Everything else is in place — the account exists, the key names it — and still nothing
    /// happens. That is the half of the decision that keeps a development convenience from being a
    /// production hole, and it is the half a happy-path test would never notice was missing.
    /// </remarks>
    [Fact]
    public async Task OutsideDevelopment_ItGrantsNothing()
    {
        await using var world = await World.CreateAsync(
            username: Username, withAccount: false, password: "a-password",
            environment: Environments.Production);

        await world.Host.EnsureAdministratorAsync();

        (await world.ExistsAsync(Username)).Should()
            .BeFalse("a known credential in production is the hole this gate exists to close");
    }

    /// <summary>
    /// With no username configured, it grants nothing.
    /// </summary>
    /// <remarks>
    /// The state of every machine that has no overrides file, this repository's CI included. It has
    /// to be a quiet no-op rather than a failure, or the committed configuration alone would refuse
    /// to start a host.
    /// </remarks>
    [Fact]
    public async Task WithNoUsernameConfigured_ItGrantsNothing()
    {
        await using var world = await World.CreateAsync(username: null, withAccount: true);

        await world.Host.EnsureAdministratorAsync();

        (await world.IsAdministratorAsync(Username)).Should().BeFalse();
    }

    /// <summary>
    /// A configured account that does not exist, is created and granted the role.
    /// </summary>
    /// <remarks>
    /// The state of a clone of this repository: the database is empty, the committed Development
    /// configuration names <c>admin</c>, and the whole point is that nothing has to be set up
    /// first. The account is created without a trainer — an administrator is an account (ADR 0051)
    /// — and nothing here creates one, which is what leaves their token without a
    /// <c>trainer_id</c>.
    /// </remarks>
    [Fact]
    public async Task AConfiguredAccountThatDoesNotExist_IsCreatedAndGrantedTheRole()
    {
        // The credential the repository actually ships, rather than an invented one: this is the
        // fact that would notice if `admin` stopped satisfying the password policy — five
        // characters, no digit, no capital — and the README started promising a sign-in that
        // refuses.
        await using var world = await World.CreateAsync(
            username: "admin", withAccount: false, password: "admin");

        await world.Host.EnsureAdministratorAsync();

        (await world.ExistsAsync("admin")).Should().BeTrue();
        (await world.IsAdministratorAsync("admin")).Should().BeTrue();
        (await world.CanSignInAsync("admin", "admin")).Should().BeTrue();
    }

    /// <summary>
    /// With no password configured, a missing account is reported rather than invented.
    /// </summary>
    /// <remarks>
    /// Creating one anyway would put a credential nobody wrote down into a database, and refusing
    /// the host would break the promise this class makes in as many words: nothing here stops an
    /// API from starting. So it says which key is missing and carries on.
    /// </remarks>
    [Fact]
    public async Task WithNoPasswordConfigured_AMissingAccountIsReportedRatherThanInvented()
    {
        await using var world = await World.CreateAsync(
            username: "nobody", withAccount: true, password: null);

        var seeding = world.Host.EnsureAdministratorAsync;

        await seeding.Should().NotThrowAsync();
        (await world.ExistsAsync("nobody")).Should().BeFalse();
    }

    /// <summary>
    /// A password Identity refuses, leaves the host standing.
    /// </summary>
    /// <remarks>
    /// The last way this can fail, and the one a developer meets by editing their overrides file:
    /// the seeder reports what Identity said and start-up continues. Asserted because the failure
    /// arrives as a <see cref="IdentityResult"/> rather than an exception, so the version that
    /// ignored it would create nothing and say nothing.
    /// </remarks>
    [Fact]
    public async Task APasswordIdentityRefuses_LeavesTheHostStanding()
    {
        await using var world = await World.CreateAsync(
            username: Username, withAccount: false, password: "x");

        var seeding = world.Host.EnsureAdministratorAsync;

        await seeding.Should().NotThrowAsync();
        (await world.ExistsAsync(Username)).Should().BeFalse();
    }

    /// <summary>
    /// An account that already exists, keeps its password.
    /// </summary>
    /// <remarks>
    /// This is a seeder, not a reset. A developer who changed their administrator's password must
    /// not find it silently put back on the next <c>dotnet run</c> — and the configured password
    /// must not be a way to take over an account that is already there.
    /// </remarks>
    [Fact]
    public async Task AnAccountThatAlreadyExists_KeepsItsPassword()
    {
        await using var world = await World.CreateAsync(
            username: Username, withAccount: true, accountPassword: "the-real-one",
            password: "the-other-one");

        await world.Host.EnsureAdministratorAsync();

        (await world.CanSignInAsync(Username, "the-real-one")).Should().BeTrue();
        (await world.CanSignInAsync(Username, "the-other-one")).Should().BeFalse();
        (await world.IsAdministratorAsync(Username)).Should().BeTrue("the role is still granted");
    }

    /// <summary>
    /// The configured account, is granted the role.
    /// </summary>
    /// <remarks>
    /// The role does not exist beforehand, which is the state of a freshly migrated database:
    /// <c>AspNetRoles</c> is empty, and a seeder that assumed otherwise would fail on exactly the
    /// machine it was written for.
    /// </remarks>
    [Fact]
    public async Task TheConfiguredAccount_IsGrantedTheRole()
    {
        await using var world = await World.CreateAsync(username: Username, withAccount: true);

        (await world.TheRoleExistsAsync()).Should().BeFalse("a freshly migrated database has no roles");

        await world.Host.EnsureAdministratorAsync();

        (await world.TheRoleExistsAsync()).Should().BeTrue();
        (await world.IsAdministratorAsync(Username)).Should().BeTrue();
    }

    /// <summary>
    /// An account that already has the role, is left alone.
    /// </summary>
    /// <remarks>
    /// The seeder runs on every start-up, so it is idempotent or it is a bug that appears on the
    /// second <c>dotnet run</c>. Identity refuses a duplicate membership by answering a failed
    /// <see cref="IdentityResult"/> rather than by throwing, so the version without the guard would
    /// pass a test that only asserted "does not throw" — hence the count.
    /// </remarks>
    [Fact]
    public async Task AnAccountThatAlreadyHasTheRole_IsLeftAlone()
    {
        await using var world = await World.CreateAsync(username: Username, withAccount: true);

        await world.Host.EnsureAdministratorAsync();
        await world.Host.EnsureAdministratorAsync();

        (await world.RolesOfAsync(Username)).Should().Equal(IdentityRoles.Administrator);
    }

    /// <summary>
    /// A host, its identity store, and the connection that keeps the in-memory database alive.
    /// </summary>
    /// <remarks>
    /// The connection is the database: SQLite discards a <c>:memory:</c> store when the last
    /// connection to it closes, so it is opened here and held until the test ends. Every scope the
    /// seeder opens gets a context over this same connection, exactly as every request shares one
    /// database in production.
    /// </remarks>
    private sealed class World : IAsyncDisposable
    {
        private World(IHost host, SqliteConnection connection)
        {
            Host = host;
            _connection = connection;
        }

        public IHost Host { get; }

        private readonly SqliteConnection _connection;

        /// <param name="username">What the configuration key names, or <see langword="null"/> for a
        /// machine that has not set it.</param>
        /// <param name="withAccount">Whether <see cref="Username"/> exists in the identity store.</param>
        /// <param name="password">What the configuration offers to create a missing account with.</param>
        /// <param name="accountPassword">What the pre-existing account was created with, which is a
        /// different thing from the one above — telling them apart is the whole of
        /// <c>AnAccountThatAlreadyExists_KeepsItsPassword</c>.</param>
        /// <param name="environment">Defaulted through a null rather than in the signature:
        /// <c>Environments.Development</c> is a static field, not a constant, and naming the
        /// environments symbolically is worth one line of indirection.</param>
        public static async Task<World> CreateAsync(
            string? username,
            bool withAccount,
            string? password = null,
            string accountPassword = "existing",
            string? environment = null)
        {
            environment ??= Environments.Development;

            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var host = new HostBuilder()
                .UseEnvironment(environment)
                .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [AdministratorSeedExtensions.UsernameConfigurationKey] = username,
                        [AdministratorSeedExtensions.PasswordConfigurationKey] = password
                    }))
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddDbContext<TrainingIdentityDbContext>(options => options.UseSqlite(connection));
                    services
                        // The product's own password rules, mirrored from AddApiIdentity. Left at
                        // Identity's defaults this world would refuse `admin` — five characters, no
                        // digit, no capital, nothing non-alphanumeric — and every fact about
                        // creating an account would then be about a policy this repository does not
                        // have, in the direction that reads as passing.
                        .AddIdentityCore<IdentityUser<Guid>>(options =>
                        {
                            options.Password.RequireUppercase = false;
                            options.Password.RequireDigit = false;
                            options.Password.RequiredLength = 4;
                            options.Password.RequireNonAlphanumeric = false;
                        })
                        .AddRoles<IdentityRole<Guid>>()
                        .AddEntityFrameworkStores<TrainingIdentityDbContext>();
                })
                .Build();

            var world = new World(host, connection);

            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TrainingIdentityDbContext>();
                await context.Database.EnsureCreatedAsync();

                if (withAccount)
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
                    await users.CreateAsync(
                        new IdentityUser<Guid>
                        {
                            UserName = Username,
                            Email = $"{Username}@example.com"
                        },
                        accountPassword);
                }
            }

            return world;
        }

        public async Task<bool> TheRoleExistsAsync()
        {
            using var scope = Host.Services.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>()
                .RoleExistsAsync(IdentityRoles.Administrator);
        }

        public async Task<bool> IsAdministratorAsync(string username) =>
            (await RolesOfAsync(username)).Contains(IdentityRoles.Administrator, StringComparer.Ordinal);

        public async Task<bool> ExistsAsync(string username)
        {
            using var scope = Host.Services.CreateScope();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

            return await users.FindByNameAsync(username) is not null;
        }

        /// <remarks>
        /// The hash rather than a sign-in: what is asserted is which password the account holds,
        /// and <c>SignInManager</c> would drag lockout and cookie machinery into a question that is
        /// only about a hash.
        /// </remarks>
        public async Task<bool> CanSignInAsync(string username, string password)
        {
            using var scope = Host.Services.CreateScope();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

            var user = await users.FindByNameAsync(username);

            return user is not null && await users.CheckPasswordAsync(user, password);
        }

        /// <remarks>
        /// A fresh scope each time, as a later request would be: reading back from the change
        /// tracker of the scope that wrote would answer from memory what the database is supposed
        /// to answer.
        /// </remarks>
        public async Task<IReadOnlyList<string>> RolesOfAsync(string username)
        {
            using var scope = Host.Services.CreateScope();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

            var user = await users.FindByNameAsync(username);

            return user is null ? [] : [.. await users.GetRolesAsync(user)];
        }

        public async ValueTask DisposeAsync()
        {
            Host.Dispose();
            await _connection.DisposeAsync();
        }
    }
}
