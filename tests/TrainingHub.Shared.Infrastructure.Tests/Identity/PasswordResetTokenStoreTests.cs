using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Identity;

/// <summary>
/// Behavior covered for the reset credential's store (ADR 0084): what is minted, what is kept,
/// and what one guarded delete does to a race.
/// </summary>
/// <remarks>
/// Against SQLite through the real Identity model and a real <see cref="UserManager{TUser}"/>
/// over it, because the properties under test live in the schema and the SQL: the primary key
/// that makes a second credential unrepresentable, the digest column a raw token cannot fit, and
/// the delete whose row count is the whole concurrency answer. A mocked store would assert none
/// of that. The clock is the test's, so expiry is a fact and not a fifteen-minute wait.
/// </remarks>
public sealed class PasswordResetTokenStoreTests : IAsyncLifetime
{
    private const string KnownAddress = "grace.hopper@example.org";
    private const string KnownUsername = "grace.hopper";
    private const string LinkBase = "https://web.test";

    /// <summary>A clock the test moves by hand, so a credential can age without anyone waiting.</summary>
    private sealed class MovableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MovableClock _clock = new();
    private TrainingIdentityDbContext _context = null!;
    private UserManager<IdentityUser<Guid>> _userManager = null!;
    private PasswordResetTokenStore _store = null!;
    private Guid _knownUserId;

    /// <summary>Opens the database, builds the Identity schema on it, and seeds one account.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingIdentityDbContext(new DbContextOptionsBuilder<TrainingIdentityDbContext>()
            .UseSqlite(_connection)
            .Options);

        await _context.Database.EnsureCreatedAsync();

        _userManager = new UserManager<IdentityUser<Guid>>(
            new UserStore<IdentityUser<Guid>, IdentityRole<Guid>, TrainingIdentityDbContext, Guid>(_context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<IdentityUser<Guid>>(),
            userValidators: [],
            passwordValidators: [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<IdentityUser<Guid>>>.Instance);

        var account = new IdentityUser<Guid> { UserName = KnownUsername, Email = KnownAddress };
        (await _userManager.CreateAsync(account)).Succeeded.Should().BeTrue();
        _knownUserId = account.Id;

        _store = new PasswordResetTokenStore(
            _userManager,
            _context,
            Options.Create(new PasswordResetOptions { LinkBaseAddress = LinkBase }),
            _clock);
    }

    /// <summary>Closes the manager, the context and, with the connection, the database.</summary>
    public async Task DisposeAsync()
    {
        _userManager.Dispose();
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Issue async, an address no account listens at, answers nothing.
    /// </summary>
    [Fact]
    public async Task IssueAsync_AnUnknownAddress_AnswersNothing()
    {
        // Act
        var invitation = await _store.IssueAsync("nobody@example.org");

        // Assert
        invitation.Should().BeNull("an unknown address must cost no row and mint no credential");
        (await Rows()).Should().BeEmpty();
    }

    /// <summary>
    /// Issue async, a known address, stores only the digest of the token the link carries.
    /// </summary>
    [Fact]
    public async Task IssueAsync_AKnownAddress_StoresOnlyTheDigestOfTheLinksToken()
    {
        // Act
        var invitation = await _store.IssueAsync(KnownAddress);

        // Assert
        invitation.Should().NotBeNull();
        invitation.Username.Should().Be(KnownUsername);
        invitation.Lifetime.Should().Be(PasswordResetToken.Lifetime,
            "the email announces the same window the database enforces");
        invitation.Link.Should().StartWith($"{LinkBase}/reset-password?token=");

        var token = TokenOf(invitation.Link);
        var row = (await Rows()).Should().ContainSingle().Subject;

        row.UserId.Should().Be(_knownUserId);
        row.TokenHash.Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            "the table holds a digest a leak cannot redeem, never the token itself");
        row.ExpiresOnUtc.Should().Be(_clock.Now.UtcDateTime + PasswordResetToken.Lifetime,
            "the fifteen-minute window is stamped at issue");
    }

    /// <summary>
    /// Issue async, a second request, replaces the only row and kills the earlier link.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ASecondRequest_ReplacesTheOnlyRowAndKillsTheEarlierLink()
    {
        // Arrange
        var first = await _store.IssueAsync(KnownAddress);

        // Act
        var second = await _store.IssueAsync(KnownAddress);

        // Assert
        (await Rows()).Should().ContainSingle("the account is the primary key, so a second live credential is unrepresentable");
        (await _store.IsCurrentAsync(_knownUserId, TokenOf(first!.Link))).Should().BeFalse(
            "issuing a new credential is what invalidates every earlier one");
        (await _store.IsCurrentAsync(_knownUserId, TokenOf(second!.Link))).Should().BeTrue();
    }

    /// <summary>
    /// Is current async, past the lifetime, answers false.
    /// </summary>
    [Fact]
    public async Task IsCurrentAsync_PastTheLifetime_AnswersFalse()
    {
        // Arrange
        var invitation = await _store.IssueAsync(KnownAddress);

        // Act
        _clock.Now += PasswordResetToken.Lifetime + TimeSpan.FromSeconds(1);

        // Assert
        (await _store.IsCurrentAsync(_knownUserId, TokenOf(invitation!.Link))).Should().BeFalse(
            "a link is valid for exactly its lifetime and not a second longer");
    }

    /// <summary>
    /// Is current async, a token that never was, answers false without throwing.
    /// </summary>
    [Fact]
    public async Task IsCurrentAsync_ATokenThatNeverWas_AnswersFalseWithoutThrowing()
    {
        // Arrange
        await _store.IssueAsync(KnownAddress);

        // Act
        var current = await _store.IsCurrentAsync(_knownUserId, "not-even-base64url éé");

        // Assert
        current.Should().BeFalse("a malformed token walks the same path as a wrong one");
    }

    /// <summary>
    /// Try redeem async, the current credential, consumes it exactly once.
    /// </summary>
    [Fact]
    public async Task TryRedeemAsync_TheCurrentCredential_ConsumesItExactlyOnce()
    {
        // Arrange
        var invitation = await _store.IssueAsync(KnownAddress);
        var token = TokenOf(invitation!.Link);

        // Act
        var first = await _store.TryRedeemAsync(_knownUserId, token);
        var second = await _store.TryRedeemAsync(_knownUserId, token);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse("a consumed credential does not exist, so the race's loser changes nothing");
        (await Rows()).Should().BeEmpty();
    }

    /// <summary>
    /// Try redeem async, past the lifetime, refuses and leaves the inert row alone.
    /// </summary>
    [Fact]
    public async Task TryRedeemAsync_PastTheLifetime_Refuses()
    {
        // Arrange
        var invitation = await _store.IssueAsync(KnownAddress);
        _clock.Now += PasswordResetToken.Lifetime + TimeSpan.FromSeconds(1);

        // Act
        var redeemed = await _store.TryRedeemAsync(_knownUserId, TokenOf(invitation!.Link));

        // Assert
        redeemed.Should().BeFalse("the delete is guarded by expiry as well as by digest");
        (await Rows()).Should().ContainSingle("an expired row is inert, kept only until the next request replaces it");
    }

    private async Task<List<PasswordResetToken>> Rows() =>
        await _context.Set<PasswordResetToken>().AsNoTracking().ToListAsync();

    private static string TokenOf(string link) =>
        Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
}
