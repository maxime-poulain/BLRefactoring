using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Configurations;

/// <summary>
/// Maps the training aggregate onto its table.
/// </summary>
public sealed class TrainingConfiguration : AggregateRootTypeConfiguration<Training, TrainingId>
{
    /// <summary>
    /// Maps the detail value objects, the topics and the per-trainer unique title.
    /// </summary>
    protected override void ConfigureAggregate(EntityTypeBuilder<Training> builder)
    {
        builder.ToTable("Training");

        builder.Property(training => training.TrainerId)
            .HasConversion(
                id => id.Value,
                value => TrainerId.Create(value));

        // The column and the rule now agree, at a hundred characters. They did not always: the
        // column was deliberately made roomier than TrainingTitle enforced, so that tightening or
        // relaxing that rule cost no schema change — slack that absorbed the move from thirty to
        // fifty without a migration. Raising the rule to meet the column spends it. Widening the
        // title again is a migration now, and one that drops and recreates the unique index below,
        // since SQL Server will not alter a column an index covers.
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

        // One column, not a table: a status is a single value, unlike the topics below. Persisted
        // as the word the domain uses rather than an ordinal — a column a human can read, and one
        // that cannot silently change meaning when a value is inserted into the middle of the set.
        builder.Property(training => training.Status)
            .HasConversion(
                status => status.Name,
                value => TrainingStatus.FromName(value))
            .HasMaxLength(20)
            .IsRequired();

        // Flattened beside the status it belongs to, and optional: a training nobody withheld has a
        // null here. The pair carries an invariant the database cannot state — the reason is present
        // if and only if the status is Withheld — which the aggregate holds instead, since it is the
        // only writer of either (ADR 0052).
        builder.ComplexProperty(training => training.WithholdingReason, reasonBuilder =>
        {
            reasonBuilder.IsRequired(false);
            reasonBuilder.Property(reason => reason.Value)
                .HasColumnName("WithholdingReason")
                .HasMaxLength(WithholdingReason.MaximumLength);
        });

        builder.OwnsMany(training => training.Topics, topicsBuilder =>
        {
            topicsBuilder.ToTable("TrainingTopic");
            topicsBuilder.Property<Guid>("Id");
            topicsBuilder.HasKey("Id");
            topicsBuilder.Property(topic => topic.Name)
                .HasMaxLength(50)
                .IsRequired();
        });

        // The business rule "a trainer cannot have two trainings with the same title"
        // is checked upfront by IUniquenessTitleChecker for fast feedback, but only a
        // unique index makes it hold under concurrency (check-then-act race).
        // The composite index also serves TrainerId-only lookups (leading column).
        builder.HasIndex(training => new { training.TrainerId, training.Title })
            .IsUnique();
    }
}
