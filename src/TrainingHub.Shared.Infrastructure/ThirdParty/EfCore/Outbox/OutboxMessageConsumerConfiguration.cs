using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;

/// <summary>
/// Maps the delivery ledger onto its table.
/// </summary>
/// <remarks>
/// Every column declared explicitly, for the same reason the envelope's are: a get-only property
/// EF Core was not told about is silently not a column. The composite key is the repository's
/// first, on purpose — a per-consumer delivery has no identity beyond the two things it joins,
/// and the pair also answers the processor's only question (all consumers of one message) as a
/// leftmost-prefix seek, so the table needs no other index. See ADR 0034.
/// </remarks>
public sealed class OutboxMessageConsumerConfiguration : IEntityTypeConfiguration<OutboxMessageConsumer>
{
    /// <summary>
    /// Maps the ledger columns, the composite key, and the cascade that ties the ledger to its
    /// envelope's lifecycle.
    /// </summary>
    public void Configure(EntityTypeBuilder<OutboxMessageConsumer> builder)
    {
        builder.ToTable("OutboxMessageConsumer");

        builder.HasKey(delivery => new { delivery.MessageId, delivery.ConsumerName });

        // The same 128 every other name column in the outbox settled on — and one of the two
        // reasons a CLR FullName was refused as ledger identity (ADR 0034).
        builder.Property(delivery => delivery.ConsumerName)
            .IsRequired()
            .HasMaxLength(128);

        // Full precision, declared explicitly — the same decision the envelope's timestamps
        // carry. See ADR 0005.
        builder.Property(delivery => delivery.DeliveredOnUtc)
            .HasPrecision(7);

        // The cascade is what lets the ledger ride the envelope's lifecycle for free: the
        // retention sweep's ExecuteDelete and any operator's DELETE reach the ledger through the
        // database engine, with no change tracker involved (ADR 0033, ADR 0034).
        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(delivery => delivery.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
