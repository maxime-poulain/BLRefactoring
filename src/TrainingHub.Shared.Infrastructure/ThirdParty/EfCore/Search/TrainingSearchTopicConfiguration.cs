using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

/// <summary>
/// Maps the search index's topics onto their table.
/// </summary>
/// <remarks>
/// The composite key is the pair itself, the tokens' shape and for the same reason (ADR 0059): a
/// topic has no identity beyond the entry it belongs to and the name it is, and the pair makes an
/// entry's topics a set rather than a list.
/// </remarks>
public sealed class TrainingSearchTopicConfiguration : IEntityTypeConfiguration<TrainingSearchTopic>
{
    /// <summary>
    /// Maps the topic's columns, the composite key and the index a browse seeks through.
    /// </summary>
    public void Configure(EntityTypeBuilder<TrainingSearchTopic> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TrainingSearchTopic");

        builder.HasKey(topic => new { topic.TrainingId, topic.Topic });

        // Room for every name the domain declares, held to it by
        // EveryTopicTheDomainDeclares_FitsTheIndexColumn: a topic wider than this column would
        // fail at index time, on the first training filed under it, and nowhere earlier.
        builder.Property(topic => topic.Topic)
            .IsRequired()
            .HasMaxLength(50);

        // The browse question's order. The key's leading column is the entry, which answers "what
        // is this training filed under"; a facet asks the opposite — which trainings, under this
        // topic — so it needs the opposite order, exactly as the tokens' second index does.
        builder.HasIndex(topic => new { topic.Topic, topic.TrainingId })
            .HasDatabaseName("IX_TrainingSearchTopic_Topic");
    }
}
