using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Configurations;

public class TrainingConfiguration : AggregateRootTypeConfiguration<Training, TrainingId>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Training> builder)
    {
        builder.ToTable("Training");

        builder.Property(training => training.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(training => training.TrainerId)
            .HasConversion(
                id => id.Value,
                value => TrainerId.Create(value));

        builder.Property(training => training.Title)
            .HasConversion(
                title => title.Value,
                value => TrainingTitle.Create(value).Match(title => title,
                    errors => null!))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(training => training.Description)
            .HasConversion(
                description => description.Value,
                value => TrainingDescription.Create(value).Match(description => description,
                    errors => null!))
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(training => training.Prerequisites)
            .HasConversion(
                prerequisites => prerequisites.Value,
                value => TrainingPrerequisites.Create(value).Match(prerequisites => prerequisites,
                    errors => null!))
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(training => training.AcquiredSkills)
            .HasConversion(
                acquiredSkills => acquiredSkills.Value,
                value => AcquiredSkills.Create(value).Match(acquiredSkills => acquiredSkills,
                    errors => null!))
            .HasMaxLength(500)
            .IsRequired();

        builder.OwnsMany(training => training.Topics, topicsBuilder =>
        {
            topicsBuilder.ToTable("TrainingTopic");
            topicsBuilder.Property<Guid>("Id");
            topicsBuilder.HasKey("Id");
            topicsBuilder.Property(topic => topic.Name)
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.HasIndex(training => training.TrainerId);
    }
}
