using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// Maps the outbox queue. Picked up by the assembly scan in <c>TrainingContext.OnModelCreating</c>.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessage");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Type).IsRequired();
        builder.Property(message => message.Payload).IsRequired();

        // Matched by microsecond precision elsewhere in the model, so ordering stays meaningful
        // between messages produced by the same request.
        builder.Property(message => message.OccurredOn).HasPrecision(6);
        builder.Property(message => message.ProcessedOn).HasPrecision(6);

        // The processor's only query is "the oldest unprocessed messages". Filtering on
        // ProcessedOn first and ordering by OccurredOn second is exactly this index, and the
        // filter keeps it small: once a message is published it leaves the index for good,
        // so the table can grow as an audit trail without slowing the queue down.
        builder
            .HasIndex(message => message.OccurredOn)
            .HasFilter("[ProcessedOn] IS NULL")
            .HasDatabaseName("IX_OutboxMessage_Pending");
    }
}
