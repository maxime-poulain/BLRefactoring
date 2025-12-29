using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
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

        builder.Property(training => training.TrainerId)
            .HasConversion(
                id => id.Value,
                value => TrainerId.Create(value));
    }
}
