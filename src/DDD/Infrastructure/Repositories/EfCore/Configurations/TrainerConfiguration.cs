using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore.Configurations;

public class TrainerConfiguration : EntityBaseConfiguration<Trainer>
{
    public override void ConfigureEntity(EntityTypeBuilder<Trainer> builder)
    {
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
    }
}
