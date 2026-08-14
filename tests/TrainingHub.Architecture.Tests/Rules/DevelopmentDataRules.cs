using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.Development;
using TrainingHub.Shared.Infrastructure.Extensions;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What keeps five hundred invented trainings out of anywhere they were not asked for (ADR 0079).
/// </summary>
/// <remarks>
/// The seeder is the most destructive thing in this repository that nobody would call destructive.
/// It creates a hundred and seventy accounts with a password committed in plain sight, sends as
/// many emails, and fills a catalog — all of which is exactly right on a developer's machine and
/// none of which has any business happening anywhere else. Three gates say so, and a gate that
/// quietly stops being named is a gate that stops existing.
/// </remarks>
public sealed class DevelopmentDataRules
{
    /// <summary>
    /// The one place the gates are written.
    /// </summary>
    private const string TheSeeder =
        "src/TrainingHub.Shared.Infrastructure/Extensions/DevelopmentSeedExtensions.cs";

    /// <summary>
    /// The three questions asked before anything is written, in the seeder's own words.
    /// </summary>
    /// <remarks>
    /// The configuration key is named as the literal rather than as the constant, because the
    /// constant is what a rename would carry along and the literal is what a deployment actually
    /// sets. A key renamed on one side and not the other is a gate that answers a question nobody
    /// asks any more, and it fails open.
    /// </remarks>
    private static readonly string[] TheGates =
    [
        "OpenApiDocumentGeneration.IsInProgress()",
        "IsDevelopment()",
        "\"DevelopmentData:Enabled\""
    ];

    /// <summary>
    /// The development dataset, only opens in development.
    /// </summary>
    /// <remarks>
    /// Read from the source rather than exercised, and deliberately so: proving the behavior would
    /// mean standing up a host per gate against a database, and what can actually be lost here is a
    /// line somebody deleted while making the seeder do one more thing. The gates are three
    /// distinct questions and each is checked on its own, so removing one is one failure rather
    /// than a general complaint.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0079",
        "the development catalog is created in Development, when asked, and never otherwise")]
    public void TheDevelopmentDataset_OnlyOpensInDevelopment()
    {
        var seeder = SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, TheSeeder.Replace('/', Path.DirectorySeparatorChar)));

        TheGates
            .Selected("gate the seeder asks before writing anything")
            .Where(gate => !seeder.Contains(gate, StringComparison.Ordinal))
            .Select(gate =>
                $"'{TheSeeder}' no longer names {gate}. Each of the three gates refuses a different " +
                "way of reaching this code — a client generation, a deployment, and a machine that " +
                "was only supposed to start — and a missing one fails open (ADR 0079)")
            .ShouldHold();
    }

    /// <summary>
    /// The seeded catalog, stays under the domain's own quota.
    /// </summary>
    /// <remarks>
    /// Both numbers are read rather than restated, so this fails whichever of the two moves. What it
    /// defends is not the seeding but what happens afterwards: a seeded trainer sitting on
    /// <c>Training.MaximumPerTrainer</c> would meet a refusal on the first training anybody created
    /// by hand, and that refusal — correct, and produced by the fixture rather than by the user —
    /// reads as a defect in the product.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0079",
        "a seeded trainer can still create a training by hand, so the seeded catalog stays under the quota")]
    public void TheSeededCatalog_StaysUnderTheDomainsOwnQuota()
    {
        new[]
        {
            (Seeded: DevelopmentDataset.MaximumTrainingsPerTrainer, Admitted: Training.MaximumPerTrainer)
        }
            .Selected("bound the seeded catalog is held to")
            .Where(bounds => bounds.Seeded >= bounds.Admitted)
            .Select(bounds =>
                $"the dataset gives a trainer up to {bounds.Seeded} trainings and the domain admits " +
                $"{bounds.Admitted}. A seeded catalog on the quota refuses the first training created " +
                "by hand, which is the rule working and looks like the product failing (ADR 0079)")
            .ShouldHold();
    }

    /// <summary>
    /// The seeded password, satisfies the production policy.
    /// </summary>
    /// <remarks>
    /// The claim ADR 0079 makes about the word the seeder ships: it passes as written, and nothing
    /// about Identity was relaxed to let it. Both halves are executable here — the options come from
    /// the real <c>AddApiIdentity</c>, and the verdict comes from the framework's own
    /// <see cref="PasswordValidator{TUser}"/> rather than from a second reading of the same six
    /// flags. Tighten the posture and this fails, which is the right way round: the fixture is what
    /// has to move.
    /// <para>
    /// The word is read from <b>the committed Development configuration of both hosts</b> rather
    /// than from a constant, because that is now where it lives and what a developer actually gets.
    /// Two failures are therefore one rule: a host whose file names no password runs the seeder,
    /// creates nobody and says so only in a log, and a word that the policy would refuse does the
    /// same thing one step later. Reading only one host would leave the other free to drift.
    /// </para>
    /// <para>
    /// The user store is a stub that answers nothing, and that is sound rather than lazy — the
    /// validator reads the manager's options and its error describer and touches no store at all.
    /// Standing up Identity's EF store here would put a database behind a question that has none.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0079",
        "the seeded password passes the production policy as written, and nothing was relaxed to let it")]
    public async Task TheSeededPassword_SatisfiesTheProductionPolicy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiIdentity(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();

        using var manager = new UserManager<IdentityUser<Guid>>(
            new UnusedUserStore(),
            provider.GetRequiredService<IOptions<IdentityOptions>>(),
            new PasswordHasher<IdentityUser<Guid>>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            provider,
            NullLogger<UserManager<IdentityUser<Guid>>>.Instance);

        var shipped = new List<(string Host, string? Password, IdentityResult? Verdict)>();

        foreach (var host in DevelopmentConfigurations)
        {
            var password = SeededPassword(host);

            shipped.Add((
                host,
                password,
                string.IsNullOrWhiteSpace(password)
                    ? null
                    : await new PasswordValidator<IdentityUser<Guid>>()
                        .ValidateAsync(manager, new IdentityUser<Guid>(), password)));
        }

        shipped
            .Selected("Development configuration a host of this API ships")
            .SelectMany(Refusals)
            .ShouldHold();
    }

    /// <summary>What one host's shipped configuration gets wrong, of the two things it can.</summary>
    private static IEnumerable<string> Refusals(
        (string Host, string? Password, IdentityResult? Verdict) shipped)
    {
        if (shipped.Verdict is null)
        {
            yield return
                $"'{shipped.Host}' names no '{DevelopmentSeedExtensions.PasswordConfigurationKey}'. A " +
                "developer who switches the catalog on gets a host that seeds nobody and mentions it in " +
                "one log line, which reads as the seeder being broken (ADR 0079)";

            yield break;
        }

        foreach (var error in shipped.Verdict.Errors)
        {
            yield return
                $"the configured password policy refuses the password '{shipped.Host}' ships: " +
                $"{error.Description} Either that file names a word this product would never accept, " +
                "or the posture was tightened and the fixture was not moved with it (ADR 0079)";
        }
    }

    /// <summary>The Development configuration each API host ships, derived from the hosts themselves.</summary>
    /// <remarks>
    /// Both files or neither: every use case exists on both hosts, and a seeding credential present
    /// on one of them is exactly the asymmetry this repository spends a rule per feature refusing.
    /// </remarks>
    private static IReadOnlyList<string> DevelopmentConfigurations { get; } =
    [
        .. SourceTree.AllFiles
            .Select(SourceTree.Relative)
            .Where(file => file.EndsWith("/Api/appsettings.Development.json", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal)
    ];

    /// <summary>What a host's committed Development configuration offers the seeder, if anything.</summary>
    private static string? SeededPassword(string configuration)
    {
        using var document = JsonDocument.Parse(
            SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, configuration)));

        var path = DevelopmentSeedExtensions.PasswordConfigurationKey.Split(':');

        var element = document.RootElement;

        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(segment, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    /// <summary>
    /// A store the validator never reaches, present because the manager's constructor demands one.
    /// </summary>
    private sealed class UnusedUserStore : IUserStore<IdentityUser<Guid>>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetUserNameAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetUserNameAsync(IdentityUser<Guid> user, string? userName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetNormalizedUserNameAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetNormalizedUserNameAsync(
            IdentityUser<Guid> user, string? normalizedName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentityResult> CreateAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentityResult> UpdateAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentityResult> DeleteAsync(IdentityUser<Guid> user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentityUser<Guid>?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IdentityUser<Guid>?> FindByNameAsync(
            string normalizedUserName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
