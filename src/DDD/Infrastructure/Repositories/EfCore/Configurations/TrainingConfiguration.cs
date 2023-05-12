using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore.Configurations;

public class TrainingConfiguration : EntityBaseConfiguration<Training>
{
    public override void ConfigureEntity(EntityTypeBuilder<Training> builder)
    {
        builder.Property(training => training.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(training => training.Title).HasMaxLength(100).IsRequired();
        builder.OwnsMany(e => e.Rates, b =>
        {
            b.WithOwner().HasForeignKey("TrainingId");
            b.Property<int>("Id");
            b.HasKey("Id");

            b.Property<int>(e => e.Value).IsRequired();
            b.Property(e => e.AuthorId).IsRequired();

            b.OwnsOne(e => e.Comment, b2 => b2.Property(e => e.Content).HasMaxLength(200));
        });
    }
}
