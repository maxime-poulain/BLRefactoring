using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Configurations;

/// <summary>
/// Maps the trainer aggregate onto its table.
/// </summary>
public sealed class TrainerConfiguration : AggregateRootTypeConfiguration<Trainer, TrainerId>
{
    /// <summary>
    /// Maps the profile value objects and the uniqueness of the contact address.
    /// </summary>
    protected override void ConfigureAggregate(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("Trainer");

        // No uniqueness constraint here, deliberately: the contact email is a
        // business attribute of the profile, not a credential. Two trainers of the
        // same organisation may legitimately publish the same address. Uniqueness
        // is enforced by Identity on the account email, which is a different value.
        builder.ComplexProperty(e => e.ContactEmail, b =>
        {
            b.Property(e => e.FullAddress)
                .HasColumnName("ContactEmail")
                .IsRequired()
                .HasMaxLength(320);
        });

        builder.ComplexProperty(e => e.Name, b =>
        {
            b.Property(e => e.Firstname)
                .HasColumnName("Firstname")
                .IsRequired()
                .HasMaxLength(50);
            b.Property(e => e.Lastname)
                .HasColumnName("Lastname")
                .IsRequired()
                .HasMaxLength(50);
        });

        // The Bio itself is optional, its Value is not: the complex property carries
        // the nullability, so the column is nullable and a null column round-trips as
        // a null Bio, while the value object keeps its promise of a non-empty Value.
        builder.ComplexProperty(e => e.Bio, bioBuilder =>
        {
            bioBuilder.IsRequired(false);
            bioBuilder.Property(e => e.Value)
                .HasColumnName("Bio")
                .HasMaxLength(500);
        });

        // Flattened into this table like every other value object on this aggregate, and
        // optional: a trainer without a photo has nulls in these three columns rather than a
        // row somewhere else. What is stored is the photo's identity — never a bucket, a key
        // or a URL — so moving the bytes to a different store changes configuration and
        // leaves this table alone. See ADR 0021.
        builder.ComplexProperty(e => e.Photo, photoBuilder =>
        {
            photoBuilder.IsRequired(false);

            // Converted like every other typed identifier on this aggregate. The column stays
            // uniqueidentifier, so typing this needed no migration — what changed is what the
            // model can confuse it with. See ADR 0044.
            photoBuilder.Property(p => p.PhotoId)
                .HasConversion(v => v.Value, v => PhotoId.Create(v))
                .HasColumnName("PhotoId");

            photoBuilder.Property(p => p.ContentType)
                .HasColumnName("PhotoContentType")
                .HasMaxLength(64);

            photoBuilder.Property(p => p.ByteSize)
                .HasColumnName("PhotoByteSize");
        });

        // Stored as the word rather than an ordinal, like the training's own status: the whole of a
        // suspension is this column, so it had better be one a reader can interpret without a
        // lookup table (ADR 0050).
        builder.Property(trainer => trainer.Status)
            .HasConversion(
                status => status.Name,
                value => TrainerStatus.FromName(value))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasConversion(v => v.Value, v => UserId.Create(v))
            .HasColumnName("UserId")
            .IsRequired();
    }
}
