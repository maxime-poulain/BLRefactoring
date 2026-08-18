using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Maps the verification credential to its table in the Identity store.
/// </summary>
/// <remarks>
/// The key is the account, not a surrogate: one row per user is the latest-only invariant of
/// ADR 0090, and making it the primary key is what turns the invariant from a discipline into a
/// constraint. The digest carries a unique index of its own, because redemption looks the row up
/// by digest — the emailed link carries the token and nothing else, so there is no account to
/// look it up by. The row follows its account out of existence by cascade.
/// <para>
/// Deliberately <em>not</em> an <c>IEntityTypeConfiguration</c>: <c>TrainingContext</c> builds
/// its model by scanning this assembly for that interface, and implementing it here would smuggle
/// the credential into the business context's model — a table its migrations do not know. The
/// Identity context applies this by name instead, exactly as it applies the reset credential's.
/// </para>
/// </remarks>
public static class EmailVerificationTokenConfiguration
{
    /// <summary>
    /// Configures the table, the account-keyed identity, the digest column and its index.
    /// </summary>
    /// <param name="builder">The builder for the entity.</param>
    public static void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailVerificationToken");

        builder.HasKey(token => token.UserId);

        builder.Property(token => token.UserId)
            .ValueGeneratedNever();

        // A SHA-256 digest is exactly 32 bytes; bounding the column documents that and keeps an
        // accidental raw token — 43 characters of Base64Url — from ever fitting the schema.
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(32);

        // Unique, because redemption's lookup key is the digest: 256 bits of entropy make a
        // collision a non-event, and the index is what keeps the lookup from scanning the table.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.Property(token => token.CreatedOnUtc)
            .HasPrecision(7);

        builder.HasOne<IdentityUser<Guid>>()
            .WithOne()
            .HasForeignKey<EmailVerificationToken>(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
