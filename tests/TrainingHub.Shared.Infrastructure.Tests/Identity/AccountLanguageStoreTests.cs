using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Identity;

/// <summary>
/// Behavior covered for the account's language preference (ADR 0091): one row per account, and an
/// absence that means the default rather than an error.
/// </summary>
/// <remarks>
/// Against SQLite through the real Identity model, for the reason the two credential suites give:
/// the properties under test live in the schema — the primary key that makes a second preference
/// unrepresentable, and the cascade that takes the row out with the account it describes. The
/// store's own read-then-write would pass against a dictionary; what it has to survive is the
/// constraint.
/// </remarks>
public sealed class AccountLanguageStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private TrainingIdentityDbContext _context = null!;
    private AccountLanguageStore _store = null!;
    private Guid _knownUserId;

    /// <summary>Opens the database, builds the Identity schema on it, and seeds one account.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingIdentityDbContext(new DbContextOptionsBuilder<TrainingIdentityDbContext>()
            .UseSqlite(_connection)
            .Options);

        await _context.Database.EnsureCreatedAsync();

        var account = new IdentityUser<Guid>
        {
            Id = Guid.CreateVersion7(),
            UserName = "grace.hopper",
            NormalizedUserName = "GRACE.HOPPER",
            Email = "grace.hopper@example.org",
            NormalizedEmail = "GRACE.HOPPER@EXAMPLE.ORG",
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        _context.Users.Add(account);
        await _context.SaveChangesAsync();

        _knownUserId = account.Id;
        _store = new AccountLanguageStore(_context);
    }

    /// <summary>Closes the context and, with the connection, the database.</summary>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Set async, an account that never chose, records its first choice.
    /// </summary>
    [Fact]
    public async Task SetAsync_AnAccountThatNeverChose_RecordsItsFirstChoice()
    {
        await _store.SetAsync(_knownUserId, "fr");

        var stored = await _context.Set<AccountLanguage>().AsNoTracking().SingleAsync();

        stored.UserId.Should().Be(_knownUserId);
        stored.Language.Should().Be("fr");
    }

    /// <summary>
    /// Set async, choosing again, replaces the choice rather than adding to it.
    /// </summary>
    /// <remarks>
    /// The invariant is the primary key's, not this method's: a second row for one account cannot
    /// exist, so the only question is whether the store updates or throws (ADR 0091).
    /// </remarks>
    [Fact]
    public async Task SetAsync_ChoosingAgain_ReplacesTheChoiceRatherThanAddingToIt()
    {
        await _store.SetAsync(_knownUserId, "fr");
        await _store.SetAsync(_knownUserId, "ru");

        var stored = await _context.Set<AccountLanguage>().AsNoTracking().ToListAsync();

        stored.Should().ContainSingle().Which.Language.Should().Be("ru");
    }

    /// <summary>
    /// By user id async, an account that stated no preference, answers nothing.
    /// </summary>
    /// <remarks>
    /// Null rather than the default, because the fallback belongs to the composer: this is the
    /// silence ADR 0091 passes through rather than fills, and every account created before this
    /// table sends it.
    /// </remarks>
    [Fact]
    public async Task ByUserIdAsync_AnAccountThatStatedNoPreference_AnswersNothing()
    {
        var language = await Query().ByUserIdAsync(_knownUserId);

        language.Should().BeNull();
    }

    /// <summary>
    /// By user id async, an account that chose, answers what it chose.
    /// </summary>
    [Fact]
    public async Task ByUserIdAsync_AnAccountThatChose_AnswersWhatItChose()
    {
        await _store.SetAsync(_knownUserId, "ru");

        var language = await Query().ByUserIdAsync(_knownUserId);

        language.Should().Be("ru");
    }

    /// <summary>
    /// The preference, follows its account out of existence.
    /// </summary>
    /// <remarks>
    /// The cascade, asserted rather than assumed: an erased account leaves no preference behind,
    /// which is the same promise the two credentials make and the reason the erasure's own notice
    /// carries its language on the fact instead of reading it here (ADR 0091).
    /// </remarks>
    [Fact]
    public async Task ThePreference_FollowsItsAccountOutOfExistence()
    {
        await _store.SetAsync(_knownUserId, "fr");

        _context.Users.Remove(await _context.Users.SingleAsync(user => user.Id == _knownUserId));
        await _context.SaveChangesAsync();

        (await _context.Set<AccountLanguage>().AsNoTracking().AnyAsync()).Should().BeFalse();
    }

    /// <summary>The read port over the same database, for the questions the store's writes answer.</summary>
    /// <remarks>
    /// The business context is null because <c>ByUserIdAsync</c> never opens it — the preference
    /// is a row of the Identity store alone, and the trainer half of that port has its own suite
    /// against the write model it projects from.
    /// </remarks>
    private AccountLanguageQuery Query() => new(null!, _context);
}
