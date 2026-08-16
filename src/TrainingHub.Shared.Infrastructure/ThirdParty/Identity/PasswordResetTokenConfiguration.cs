using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Maps the reset credential to its table in the Identity store.
/// </summary>
/// <remarks>
/// The key is the account, not a surrogate: one row per user is the latest-only invariant of
/// ADR 0084, and making it the primary key is what turns the invariant from a discipline into a
/// constraint. The row follows its account out of existence by cascade, so deleting a user can
/// never leave a redeemable credential behind.
/// <para>
/// Deliberately <em>not</em> an <c>IEntityTypeConfiguration</c>: <c>TrainingContext</c> builds
/// its model by scanning this assembly for that interface, and implementing it here would smuggle
/// the credential into the business context's model — a table its migrations do not know. The
/// Identity context applies this by name instead, which is also the honest statement of who owns
/// the row.
/// </para>
/// </remarks>
public static class PasswordResetTokenConfiguration
{
    /// <summary>
    /// Configures the table, the account-keyed identity and the digest column.
    /// </summary>
    /// <param name="builder">The builder for the entity.</param>
    public static void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PasswordResetToken");

        builder.HasKey(token => token.UserId);

        builder.Property(token => token.UserId)
            .ValueGeneratedNever();

        // A SHA-256 digest is exactly 32 bytes; bounding the column documents that and keeps an
        // accidental raw token — 43 characters of Base64Url — from ever fitting the schema.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(token => token.CreatedOnUtc)
            .HasPrecision(7);

        builder.Property(token => token.ExpiresOnUtc)
            .HasPrecision(7);

        builder.HasOne<IdentityUser<Guid>>()
            .WithOne()
            .HasForeignKey<PasswordResetToken>(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
