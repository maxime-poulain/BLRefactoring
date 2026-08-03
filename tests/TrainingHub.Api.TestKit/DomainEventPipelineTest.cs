using AwesomeAssertions;
using TrainingHub.Shared;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves that domain events really are dispatched during <c>SaveChanges</c>, and that a handler's
/// own writes are persisted by the very same commit.
/// </summary>
/// <remarks>
/// <para>
/// This used to be asserted over HTTP, through the endpoint that deleted a trainer. That endpoint
/// is gone — removing a trainer is an administrative decision, not something a trainer performs on
/// themselves — and with it went the only end-to-end proof that <c>DomainEventInterceptor</c> is
/// wired at all: every other handler in the solution merely writes to the log, so nothing else is
/// observable from outside. The proof moves down rather than disappearing.
/// </para>
/// <para>
/// The trainer-deletion cascade remains the sharpest available probe: no foreign key cascades
/// <c>Trainer</c> to <c>Training</c>, deliberately, so a training that vanishes can only have been
/// removed by <c>DeleteTrainingWhenTrainerDeletedEventHandler</c> — which means the event was
/// collected from the aggregate, published, and its staged deletions swept into the same
/// transaction.
/// </para>
/// <para>
/// Each stage runs in its own scope, as separate requests would: a single scope would let the
/// change tracker answer from memory what the database is supposed to answer.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since what is under test here is
/// each host's own wiring, not the shared code it wires.</typeparam>
public abstract class DomainEventPipelineTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource
{
    /// <summary>
    /// Deleting a trainer, removes their trainings, within the same commit.
    /// </summary>
    [Fact]
    public async Task DeletingATrainer_RemovesTheirTrainings_WithinTheSameCommit()
    {
        var trainerId = TrainerId.Generate();
        await SeedTrainerWithOneTrainingAsync(trainerId);

        using (var scope = Factory.CreateScope())
        {
            var trainers = scope.ServiceProvider.GetRequiredService<ITrainerRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var trainer = await trainers.GetByIdAsync(trainerId);
            trainer.Should().NotBeNull();

            // The aggregate only announces the fact. Nothing here deletes a training.
            trainer.MarkForDeletion();
            trainers.Delete(trainer);

            await unitOfWork.SaveChangesAsync();
        }

        using (var scope = Factory.CreateScope())
        {
            var trainings = scope.ServiceProvider.GetRequiredService<ITrainingRepository>();

            var remaining = await trainings.GetByTrainerIdAsync(trainerId);

            remaining.Should().BeEmpty();
        }
    }

    private async Task SeedTrainerWithOneTrainingAsync(TrainerId trainerId)
    {
        using var scope = Factory.CreateScope();
        var trainers = scope.ServiceProvider.GetRequiredService<ITrainerRepository>();
        var trainings = scope.ServiceProvider.GetRequiredService<ITrainingRepository>();
        var titleChecker = scope.ServiceProvider.GetRequiredService<IUniquenessTitleChecker>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        trainers.Add(Trainer.Create(
            trainerId,
            UserId.Generate(),
            Required(Name.Create("Ada", "Lovelace")),
            Required(Email.Create("ada.lovelace@example.com")),
            bio: null));

        var training = Required(await Training.CreateAsync(
            TrainingId.Generate(),
            trainerId,
            Required(TrainingTitle.Create("A Training To Cascade")),
            Required(TrainingDescription.Create("A valid training description for testing purposes")),
            Required(TrainingPrerequisites.Create("Basic programming knowledge required")),
            Required(AcquiredSkills.Create("Advanced design patterns mastery")),
            [Topic.Programming],
            titleChecker));

        trainings.Add(training);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Unwraps a result the fixture expects to succeed. A failure here is a broken test, not a
    /// failing assertion, so it throws rather than reporting.
    /// </summary>
    private static T Required<T>(Result<T> result) => result.Match(
        value => value,
        errors => throw new InvalidOperationException(
            $"The fixture built an invalid value: {string.Join("; ", errors)}"));
}
