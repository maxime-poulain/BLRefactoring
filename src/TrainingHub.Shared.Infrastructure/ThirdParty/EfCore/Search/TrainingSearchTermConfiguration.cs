using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// Maps the search index's tokens onto their table.
/// </summary>
/// <remarks>
/// The composite key is the pair itself, the delivery ledger's shape and for the same reason
/// (ADR 0034): a token has no identity beyond the entry it belongs to and the word it is. It also
/// makes a title's tokens a set rather than a list, so a title repeating a word indexes it once.
/// </remarks>
public sealed class TrainingSearchTermConfiguration : IEntityTypeConfiguration<TrainingSearchTerm>
{
    /// <summary>
    /// Maps the token's columns, the composite key and the index a search seeks through.
    /// </summary>
    public void Configure(EntityTypeBuilder<TrainingSearchTerm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TrainingSearchTerm");

        builder.HasKey(term => new { term.TrainingId, term.Term });

        builder.Property(term => term.Term)
            .IsRequired()
            .HasMaxLength(100);

        // The whole point of the table. The key's leading column is the entry, which answers "what
        // is this training found by"; a search asks the opposite question, so it needs the opposite
        // order. With the term leading, a token match is a range seek rather than a scan — which is
        // the difference between this and the LIKE '%term%' ADR 0055 recorded as unseekable.
        builder.HasIndex(term => new { term.Term, term.TrainingId })
            .HasDatabaseName("IX_TrainingSearchTerm_Term");
    }
}
