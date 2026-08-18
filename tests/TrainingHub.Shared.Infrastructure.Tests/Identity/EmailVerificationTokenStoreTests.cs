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
/// Behavior covered for the verification credential's store (ADR 0090): what is minted, what is
/// kept, and what one guarded delete does to a race.
/// </summary>
/// <remarks>
/// Against SQLite through the real Identity model and a real <see cref="UserManager{TUser}"/>
/// over it, for the reason the reset store's suite gives: the properties under test live in the
/// schema and the SQL — the primary key that makes a second credential unrepresentable, the
/// digest column a raw token cannot fit, the unique index redemption looks the row up by, and
/// the delete whose row count is the whole concurrency answer. There is no expiry fact here, and
/// that absence is itself the decision under test: nothing in this store reads a clock to refuse.
/// </remarks>
public sealed class EmailVerificationTokenStoreTests : IAsyncLifetime
{
    private const string KnownAddress = "grace.hopper@example.org";
    private const string KnownUsername = "grace.hopper";
    private const string LinkBase = "https://web.test";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private TrainingIdentityDbContext _context = null!;
    private UserManager<IdentityUser<Guid>> _userManager = null!;
    private EmailVerificationTokenStore _store = null!;
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

        _store = new EmailVerificationTokenStore(
            _userManager,
            _context,
            Options.Create(new EmailVerificationOptions { LinkBaseAddress = LinkBase }),
            TimeProvider.System);
    }

    /// <summary>Closes the manager, the context and, with the connection, the database.</summary>
    public async Task DisposeAsync()
    {
        _userManager.Dispose();
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Issue async, an unknown account, answers nothing.
    /// </summary>
    [Fact]
    public async Task IssueAsync_AnUnknownAccount_AnswersNothing()
    {
        var invitation = await _store.IssueAsync(Guid.NewGuid());

        invitation.Should().BeNull("an account that is gone has no address to prove");
        (await Rows()).Should().BeEmpty();
    }

    /// <summary>
    /// Issue async, an already confirmed account, answers nothing.
    /// </summary>
    /// <remarks>
    /// The silent absorption two callers rely on: a resend that lost the race with the click,
    /// and an outbox retry re-running a delivery whose account verified in the meantime — both
    /// end here, with no email and no error (ADR 0090).
    /// </remarks>
    [Fact]
    public async Task IssueAsync_AnAlreadyConfirmedAccount_AnswersNothing()
    {
        var account = await _userManager.FindByIdAsync(_knownUserId.ToString());
        account!.EmailConfirmed = true;
        (await _userManager.UpdateAsync(account)).Succeeded.Should().BeTrue();

        var invitation = await _store.IssueAsync(_knownUserId);

        invitation.Should().BeNull("a proven address has nothing left to prove");
        (await Rows()).Should().BeEmpty();
    }

    /// <summary>
    /// Issue async, an unverified account, stores only the digest of the links token.
    /// </summary>
    [Fact]
    public async Task IssueAsync_AnUnverifiedAccount_StoresOnlyTheDigestOfTheLinksToken()
    {
        var invitation = await _store.IssueAsync(_knownUserId);

        invitation.Should().NotBeNull();
        invitation.Username.Should().Be(KnownUsername);
        invitation.EmailAddress.Should().Be(
            KnownAddress,
            "the mint resolves the delivery address from the account it just read, so the " +
            "integration event never has to carry one (ADR 0056)");
        invitation.Link.Should().StartWith($"{LinkBase}/verify-email?token=");

        var row = (await Rows()).Should().ContainSingle().Subject;
        row.UserId.Should().Be(_knownUserId);
        row.TokenHash.Should().Equal(
            SHA256.HashData(Encoding.UTF8.GetBytes(TokenOf(invitation.Link))),
            "the digest is all that rests — the token itself lives only in the link");
    }

    /// <summary>
    /// Issue async, a second request, replaces the only row and kills the earlier link.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ASecondRequest_ReplacesTheOnlyRowAndKillsTheEarlierLink()
    {
        var first = await _store.IssueAsync(_knownUserId);
        var second = await _store.IssueAsync(_knownUserId);

        (await Rows()).Should().ContainSingle("the primary key is the account — asking again replaces");

        (await _store.TryRedeemAsync(TokenOf(first!.Link)))
            .Should().BeNull("only the link from the most recent email works");
        (await _store.TryRedeemAsync(TokenOf(second!.Link)))
            .Should().Be(_knownUserId);
    }

    /// <summary>
    /// Try redeem async, the current credential, consumes it exactly once.
    /// </summary>
    [Fact]
    public async Task TryRedeemAsync_TheCurrentCredential_ConsumesItExactlyOnce()
    {
        var invitation = await _store.IssueAsync(_knownUserId);
        var token = TokenOf(invitation!.Link);

        (await _store.TryRedeemAsync(token)).Should().Be(_knownUserId);
        (await _store.TryRedeemAsync(token)).Should().BeNull(
            "consumption is deletion, so a replayed link finds nothing to spend");
        (await Rows()).Should().BeEmpty();
    }

    /// <summary>
    /// Try redeem async, a token that never was, answers null without throwing.
    /// </summary>
    [Fact]
    public async Task TryRedeemAsync_ATokenThatNeverWas_AnswersNullWithoutThrowing()
    {
        await _store.IssueAsync(_knownUserId);

        (await _store.TryRedeemAsync("not-even-base64url!!")).Should().BeNull(
            "a malformed token digests to something that matches nothing");
        (await _store.TryRedeemAsync(new string('A', 43))).Should().BeNull(
            "a well-formed guess is still a guess against 256 bits");
        (await Rows()).Should().ContainSingle("a miss burns nothing");
    }

    private async Task<List<EmailVerificationToken>> Rows() =>
        await _context.Set<EmailVerificationToken>().AsNoTracking().ToListAsync();

    private static string TokenOf(string link) =>
        Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
}
