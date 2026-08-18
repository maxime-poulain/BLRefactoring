using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// Makes sure the administrator the configuration names exists and holds the role — in
/// Development, and nowhere else.
/// </summary>
/// <remarks>
/// There is no self-service path to becoming an administrator and deliberately no endpoint that
/// grants a role (ADR 0051): administrators are made once, by whoever runs the system. That leaves
/// the question of how the first one comes into being on a machine where nobody has run anything
/// yet, and the answer is the shape ADR 0003 already chose for applying migrations — a convenience
/// of the development loop, gated on the environment, because it would be a hole anywhere else.
/// <para>
/// The committed Development configuration names <c>admin</c> with the password <c>admin</c>, so a
/// clone of this repository has a working administrator after <c>docker compose up</c> and
/// <c>dotnet run</c>, with nothing to set up. That is a fixture rather than a secret: it is a
/// well-known credential, which is exactly why the environment gate above it is the whole of its
/// safety. A deployment reaches this method only by running as Development, and no committed file
/// outside <c>appsettings.Development.json</c> names the section at all.
/// </para>
/// <para>
/// The account it creates is <em>nobody's trainer</em>, which is the record's own claim made
/// concrete: an administrator is an account, and the token they receive carries no
/// <c>trainer_id</c>. Naming another username in <c>appsettings.Local.json</c> (ADR 0035) overrides
/// the default, and an account that already exists keeps its password — this is a seeder, not a
/// reset.
/// </para>
/// <para>
/// Nothing here refuses a host. A missing key, a password Identity rejects, an account it declines
/// to create: each is reported and start-up continues. The alternative would let a stale line in
/// one developer's overrides file stop the API from starting, which is a steep price for a
/// convenience.
/// </para>
/// </remarks>
public static class AdministratorSeedExtensions
{
    /// <summary>
    /// The configuration key naming the account that holds the role.
    /// </summary>
    public const string UsernameConfigurationKey = "Administrator:Username";

    /// <summary>
    /// The configuration key carrying the password the account is created with, when it does not
    /// exist yet.
    /// </summary>
    /// <remarks>
    /// Read only to create a missing account. An account that is already there keeps whatever
    /// password it has, so a developer who changed theirs does not find it silently reset on the
    /// next start-up — and so this key cannot be used to take over an existing account.
    /// </remarks>
    public const string PasswordConfigurationKey = "Administrator:Password";

    /// <summary>
    /// In Development, ensures the administrator role exists, that the configured account exists,
    /// and that it holds the role. Everywhere else, does nothing.
    /// </summary>
    /// <param name="app">The host whose services hold Identity.</param>
    /// <remarks>
    /// No cancellation token, unlike its neighbor <see cref="DatabaseMigrationExtensions"/>:
    /// Identity's <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> accept
    /// none on any of the four calls below, and a parameter this method could only ignore would be
    /// a promise it cannot keep.
    /// </remarks>
    public static async Task EnsureAdministratorAsync(this IHost app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Emitting the OpenAPI document loads this host for real, exactly as it does for the
        // migrations above — see OpenApiDocumentGeneration. Without this, generating a client would
        // write a role into whatever database the configuration points at.
        if (OpenApiDocumentGeneration.IsInProgress())
        {
            return;
        }

        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AdministratorSeedExtensions).FullName!);

        var configuration = app.Services.GetRequiredService<IConfiguration>();

        var username = configuration[UsernameConfigurationKey];
        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogInformation(
                "No {Key} is configured, so no account was promoted. Name one in "
                + "appsettings.Local.json to work on the administration surface.",
                UsernameConfigurationKey);
            return;
        }

        // Identity's managers are scoped, and the application's root provider is not a scope.
        using var scope = app.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(IdentityRoles.Administrator))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(IdentityRoles.Administrator));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = await CreateAsync(userManager, logger, username, configuration[PasswordConfigurationKey]);

            if (user is null)
            {
                return;
            }
        }

        if (await userManager.IsInRoleAsync(user, IdentityRoles.Administrator))
        {
            return;
        }

        await userManager.AddToRoleAsync(user, IdentityRoles.Administrator);

        logger.LogInformation(
            "'{Username}' was granted the {Role} role. Sign in again: the role travels in the token.",
            username,
            IdentityRoles.Administrator);
    }

    /// <summary>
    /// Creates the configured account, or explains why it could not be, and answers
    /// <see langword="null"/> in that case.
    /// </summary>
    /// <remarks>
    /// No trainer is created alongside it, and that is the point rather than an omission: an
    /// administrator is an account and not a trainer (ADR 0051), so this is the first account in
    /// the system whose token carries no <c>trainer_id</c>.
    /// <para>
    /// The address is derived rather than configured. Identity insists on a unique one, nothing
    /// sends to it — a seeded administrator receives no welcome email, having registered through no
    /// endpoint — and a second key to keep in step buys nothing.
    /// </para>
    /// </remarks>
    private static async Task<IdentityUser<Guid>?> CreateAsync(
        UserManager<IdentityUser<Guid>> userManager,
        ILogger logger,
        string username,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            // Named rather than swallowed, and not invented either: without a password there is no
            // account to create, and choosing one here would put a credential nobody wrote down
            // into a database.
            logger.LogWarning(
                "{Key} names '{Username}', no such account exists, and no {PasswordKey} is "
                + "configured to create one with. Register it, or set that key, then restart.",
                UsernameConfigurationKey,
                username,
                PasswordConfigurationKey);
            return null;
        }

        var user = new IdentityUser<Guid>
        {
            UserName = username,
            // Confirmed at birth: a seeded account has no mailbox to click from, and an
            // administrator is provisioned by hand rather than through registration (ADR 0090).
            EmailConfirmed = true,
            Email = $"{username}@traininghub.local"
        };

        var creation = await userManager.CreateAsync(user, password);
        if (!creation.Succeeded)
        {
            // Identity's own words, which name the rule that refused — a password below the
            // configured length, a username with an illegal character. The password itself is
            // never written to the log.
            logger.LogWarning(
                "The administrator '{Username}' could not be created: {Reasons}",
                username,
                string.Join(", ", creation.Errors.Select(error => error.Description)));
            return null;
        }

        logger.LogInformation(
            "The administrator '{Username}' was created. It is nobody's trainer, so its token "
            + "carries no trainer_id and the trainer endpoints refuse it.",
            username);

        return user;
    }
}
