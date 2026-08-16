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
    /// and the reset credential of ADR 0084 is a guest in that store, keyed to its
    /// <c>AspNetUsers</c> row. Applied by name rather than through the configuration interface,
    /// for the reason the configuration itself records: the business context scans this assembly
    /// for that interface, and the credential belongs to this model alone.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        PasswordResetTokenConfiguration.Configure(builder.Entity<PasswordResetToken>());
    }
}
