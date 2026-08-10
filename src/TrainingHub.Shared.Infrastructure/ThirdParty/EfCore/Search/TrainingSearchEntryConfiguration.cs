using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// Maps the search index's entries onto their table.
/// </summary>
/// <remarks>
/// Every column declared explicitly, for the same reason the outbox's are: a get-only property
/// EF Core was not told about is silently not a column. The title is a plain column here — no value
/// converter, no complex property — because this is the index's own copy of it, and a converted
/// column is one EF Core can compare for equality and cannot look inside (ADR 0055, ADR 0059).
/// </remarks>
public sealed class TrainingSearchEntryConfiguration : IEntityTypeConfiguration<TrainingSearchEntry>
{
    /// <summary>
    /// Maps the entry's columns, its key, its tokens and the two indexes it is read through.
    /// </summary>
    public void Configure(EntityTypeBuilder<TrainingSearchEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TrainingSearchEntry");

        builder.HasKey(entry => entry.TrainingId);

        // Not value-generated: the identity is the training's, handed over by the fact.
        builder.Property(entry => entry.TrainingId)
            .ValueGeneratedNever();

        // The same hundred TrainingTitle enforces. The index holds what the write model holds, and
        // a copy that could not hold every title would be an index that loses documents.
        builder.Property(entry => entry.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(entry => entry.IsPublished)
            .IsRequired();

        builder.Property(entry => entry.IsTrainerHidden)
            .IsRequired();

        builder.HasMany(entry => entry.Terms)
            .WithOne()
            .HasForeignKey(term => term.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        // The sanction's index: hiding or showing a trainer's catalog is one statement over this
        // one, which is what lets the pair cost a call each way rather than a rebuild (ADR 0056).
        builder.HasIndex(entry => entry.TrainerId);

        // The listing's index: a search with no term is the offered catalog in its total order, so
        // the two visibility columns lead and the order follows. A page is then a seek and a walk,
        // never a sort of everything the table holds (ADR 0029, ADR 0059).
        builder.HasIndex(entry => new
        {
            entry.IsPublished,
            entry.IsTrainerHidden,
            entry.Title,
            entry.TrainingId
        }).HasDatabaseName("IX_TrainingSearchEntry_Offered");
    }
}
