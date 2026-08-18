using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Maps the account's language preference to its table in the Identity store.
/// </summary>
/// <remarks>
/// The key is the account, because one account reads in one language: making it the primary key
/// turns that from a discipline into a constraint (ADR 0091). The row follows its account out of
/// existence by cascade, like the two credentials configured beside it.
/// <para>
/// Deliberately <em>not</em> an <c>IEntityTypeConfiguration</c>, for the reason those two record:
/// <c>TrainingContext</c> builds its model by scanning this assembly for that interface, and this
/// table belongs to the Identity model alone. The Identity context applies it by name.
/// </para>
/// </remarks>
public static class AccountLanguageConfiguration
{
    /// <summary>
    /// Configures the table, the account-keyed identity and the language column.
    /// </summary>
    /// <param name="builder">The builder for the entity.</param>
    public static void Configure(EntityTypeBuilder<AccountLanguage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccountLanguage");

        builder.HasKey(preference => preference.UserId);

        builder.Property(preference => preference.UserId)
            .ValueGeneratedNever();

        // Sixteen characters is room for a regional code and its script — "zh-Hant-TW" is ten —
        // without pretending the column could hold a sentence. The application offers neutral
        // codes today (ADR 0088); the width is what would let a regional one arrive without a
        // migration.
        builder.Property(preference => preference.Language)
            .IsRequired()
            .HasMaxLength(16);

        builder.HasOne<IdentityUser<Guid>>()
            .WithOne()
            .HasForeignKey<AccountLanguage>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
