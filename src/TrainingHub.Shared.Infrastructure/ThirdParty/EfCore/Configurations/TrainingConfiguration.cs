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

        // A complex property of one member, where ADR 0032 says a single scalar converts — the one
        // exception that record now carries, and ADR 0060 is why. A converted column is one EF Core
        // compares for equality without being able to look inside: `Title.Value.Contains(term)`
        // answers "could not be translated" on a conversion and translates on this. The whole
        // administrative search rests on that one difference.
        //
        // The column and the rule agree at a hundred characters. They did not always: the column was
        // deliberately made roomier than TrainingTitle enforced, so that tightening or relaxing that
        // rule cost no schema change — slack that absorbed the move from thirty to fifty without a
        // migration. Raising the rule to meet the column spent it.
        builder.ComplexProperty(training => training.Title, titleBuilder =>
        {
            titleBuilder.Property(title => title.Value)
                .HasColumnName("Title")
                .HasMaxLength(100)
                .IsRequired();
        });

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

        // The business rule "a trainer cannot have two trainings with the same title" is checked
        // upfront by IUniquenessTitleChecker for fast feedback, and only a unique index makes it
        // hold under concurrency (check-then-act race). That index is still there, on
        // (TrainerId, Title), created by 20260730163126_AddUniqueTrainingTitlePerTrainer — but it is
        // no longer declared here, and this absence is a decision rather than an omission.
        //
        // EF Core 10 cannot index a scalar nested in a complex type; asked to, it answers "The
        // property or navigation 'Title' cannot be added to the 'Training' type because a property
        // or navigation with the same name already exists". The capability lands in EF Core 11.
        // So the model gives up describing the index and the database keeps enforcing it, which
        // changes nothing a caller sees: UnitOfWork still turns SQL error 2601 or 2627 into
        // Training.DuplicateTitle, and the same 409 comes back (ADR 0060).
        //
        // What guards it now is Create_DuplicateTitleForSameTrainer_Returns409 in the TestKit, run
        // by both hosts against a migrated SQL Server, and an architecture rule holding the
        // migration that creates it. Nothing here can, and pretending otherwise would be worse.
    }
}
