using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Represents the database context for identity management, including users and roles.
/// Inherits from <see cref="IdentityDbContext{TUser, TRole, TKey}"/> to provide identity-related functionality.
/// </summary>
public sealed class TrainingIdentityDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrainingIdentityDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the database context.</param>
    public TrainingIdentityDbContext(DbContextOptions<TrainingIdentityDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Adds this repository's one table to the framework's own model.
    /// </summary>
    /// <param name="builder">The builder for the model.</param>
    /// <remarks>
    /// The base call comes first and is not optional: it is what shapes the seven Identity tables,
    /// and the three guests below are keyed to their <c>AspNetUsers</c> row — the reset credential
    /// of ADR 0084, the verification credential of ADR 0090, and the language preference of
    /// ADR 0091. Each is applied by name rather than through the configuration interface, for the
    /// reason the configurations themselves record: the business context scans this assembly for
    /// that interface, and these tables belong to this model alone.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        PasswordResetTokenConfiguration.Configure(builder.Entity<PasswordResetToken>());
        EmailVerificationTokenConfiguration.Configure(builder.Entity<EmailVerificationToken>());
        AccountLanguageConfiguration.Configure(builder.Entity<AccountLanguage>());
    }
}
