using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore.Configurations;

public class TrainingConfiguration : AggregateRootTypeConfiguration<Training, TrainingId>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Training> builder)
    {
        builder.ToTable("Training");

        builder.Property(training => training.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(training => training.TrainerId);

        builder.OwnsMany(e => e.Rates, b =>
        {
            b.WithOwner().HasForeignKey("TrainingId");
            b.Property<int>("Id");
            b.HasKey("Id");

            b.Property<int>(e => e.Value).IsRequired();
            b.Property(e => e.AuthorId).IsRequired();

            b.OwnsOne(e => e.Comment,
                commentBuilder => commentBuilder.Property(comment => comment.Content).HasMaxLength(200));
        });
    }
}
