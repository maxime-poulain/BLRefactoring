using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Configurations;

public class TrainerConfiguration : AggregateRootTypeConfiguration<Trainer, TrainerId>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("Trainer");

        builder.OwnsOne(e => e.Email, b =>
        {
            b.Property(e => e.FullAddress)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(320);
        });

        builder.OwnsOne(e => e.Name, b =>
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

        builder.Property(p => p.UserId)
            .HasConversion(v => v.Value, v => UserId.Create(v))
            .HasColumnName("UserId")
            .IsRequired();
    }
}
