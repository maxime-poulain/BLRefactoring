using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Search;

/// <summary>
/// What the four operations of the search port do now that something is behind them.
/// </summary>
/// <remarks>
/// Every caller is an outbox consumer reading a committed fact that a lapsed lease may deliver
/// again (ADR 0025, ADR 0034), so "safe to run twice" is asserted rather than assumed — for the
/// upsert, for the removal and for both halves of the sanction.
/// </remarks>
public sealed class TrainingSearchIndexerTests : SearchIndexTest
{
    /// <summary>
    /// Index async, a published training, writes it with the words of its title.
    /// </summary>
    /// <remarks>
    /// The tokens are what make this an index rather than a copy, so they are the assertion. The
    /// entry's own visibility columns are asserted with them, because an entry written invisible
    /// would be an index nobody could tell from an empty one.
    /// </remarks>
    [Fact]
    public async Task IndexAsync_APublishedTraining_WritesItWithTheWordsOfItsTitle()
    {
        var trainer = await GivenTrainerAsync();
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");

        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);

        var entry = await EntryOfAsync(training.Id.Value);

        entry.Should().NotBeNull();
        entry!.Title.Should().Be("Domain Driven Design");
        entry.TrainerId.Should().Be(trainer.Id.Value);
        entry.IsPublished.Should().BeTrue();
        entry.IsTrainerHidden.Should().BeFalse();
        entry.Terms.Select(term => term.Term).Should().BeEquivalentTo("DOMAIN", "DRIVEN", "DESIGN");
    }

    /// <summary>
    /// Index async, an unpublished training, writes it out of public view.
    /// </summary>
    /// <remarks>
    /// A creation is announced and indexed before anybody publishes anything, so the index holds
    /// entries that are not on offer. Composing visibility at write time is what lets it: the row
    /// exists, and the reader is the one that refuses to serve it.
    /// </remarks>
    [Fact]
    public async Task IndexAsync_AnUnpublishedTraining_WritesItOutOfPublicView()
    {
        var trainer = await GivenTrainerAsync();
        var training = await GivenTrainingAsync(trainer, "Not Yet Offered", published: false);

        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);

        var entry = await EntryOfAsync(training.Id.Value);

        entry.Should().NotBeNull();
        entry!.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Index async, a training whose owner is suspended, writes it hidden.
    /// </summary>
    /// <remarks>
    /// The reason the read-back takes the trainer as well as the training: a transfer can hand a
    /// training to a suspended trainer, and an entry that inherited nothing would put a sanctioned
    /// catalog back in front of the public one training at a time.
    /// </remarks>
    [Fact]
    public async Task IndexAsync_ATrainingWhoseOwnerIsSuspended_WritesItHidden()
    {
        var trainer = await GivenTrainerAsync(suspended: true);
        var training = await GivenTrainingAsync(trainer, "Under Sanction");

        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);

        var entry = await EntryOfAsync(training.Id.Value);

        entry.Should().NotBeNull();
        entry!.IsTrainerHidden.Should().BeTrue();
    }

    /// <summary>
    /// Index async, the same fact twice, converges on one entry.
    /// </summary>
    /// <remarks>
    /// The property ADR 0024 relies on when it calls the index pair idempotent, and the one a
    /// composite key would turn into a failed insert rather than a duplicate: re-indexing rewrites
    /// the tokens instead of adding them.
    /// </remarks>
    [Fact]
    public async Task IndexAsync_TheSameFactTwice_ConvergesOnOneEntry()
    {
        var trainer = await GivenTrainerAsync();
        var training = await GivenTrainingAsync(trainer, "Event Storming");

        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);
        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);

        Context.ChangeTracker.Clear();

        (await Context.Set<TrainingSearchEntry>().CountAsync()).Should().Be(1);
        (await Context.Set<TrainingSearchTerm>().CountAsync()).Should().Be(2);
    }

    /// <summary>
    /// Index async, a training the write model no longer holds, writes nothing.
    /// </summary>
    /// <remarks>
    /// The ordering a delivery ledger cannot rule out: a creation redelivered after the deletion
    /// has already been consumed. Inventing an entry here would race a training back into the
    /// catalog after it was deleted, which is the defect ADR 0050 closed from the other end.
    /// </remarks>
    [Fact]
    public async Task IndexAsync_ATrainingTheWriteModelNoLongerHolds_WritesNothing()
    {
        var trainer = await GivenTrainerAsync();
        var gone = Guid.NewGuid();

        await Indexer.IndexAsync(gone, trainer.Id.Value);

        (await EntryOfAsync(gone)).Should().BeNull();
    }

    /// <summary>
    /// Remove async, an indexed training, takes its words with it.
    /// </summary>
    /// <remarks>
    /// Through the cascading foreign key rather than through the tracker, so the tokens cannot
    /// outlive the entry that named them — the shape the outbox's ledger already rides (ADR 0034).
    /// </remarks>
    [Fact]
    public async Task RemoveAsync_AnIndexedTraining_TakesItsWordsWithIt()
    {
        var trainer = await GivenTrainerAsync();
        var training = await GivenTrainingAsync(trainer, "Withdrawn Soon");

        await Indexer.IndexAsync(training.Id.Value, trainer.Id.Value);
        await Indexer.RemoveAsync(training.Id.Value);

        Context.ChangeTracker.Clear();

        (await EntryOfAsync(training.Id.Value)).Should().BeNull();
        (await Context.Set<TrainingSearchTerm>().CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Remove async, a training the index never held, is not an error.
    /// </summary>
    /// <remarks>
    /// The port promises it, because a withdrawal and a deletion are separate facts that can both
    /// reach the index for the same training.
    /// </remarks>
    [Fact]
    public async Task RemoveAsync_ATrainingTheIndexNeverHeld_IsNotAnError()
    {
        var removing = async () => await Indexer.RemoveAsync(Guid.NewGuid());

        await removing.Should().NotThrowAsync();
    }

    /// <summary>
    /// Hide trainer catalog async, a trainer with several trainings, hides all of them and
    /// forgets none.
    /// </summary>
    /// <remarks>
    /// The promise ADR 0056 made on behalf of a real adapter: one call about a trainer, and each
    /// training's own publication untouched. The second half is the one worth asserting — a hiding
    /// that also cleared <c>IsPublished</c> would pass every visibility assertion and lose the
    /// catalog at the lifting.
    /// </remarks>
    [Fact]
    public async Task HideTrainerCatalogAsync_ATrainerWithSeveralTrainings_HidesAllOfThemAndForgetsNone()
    {
        var trainer = await GivenTrainerAsync();
        var offered = await GivenTrainingAsync(trainer, "First Offer");
        var withdrawn = await GivenTrainingAsync(trainer, "Second Offer", published: false);

        await Indexer.IndexAsync(offered.Id.Value, trainer.Id.Value);
        await Indexer.IndexAsync(withdrawn.Id.Value, trainer.Id.Value);

        await Indexer.HideTrainerCatalogAsync(trainer.Id.Value);

        (await EntryOfAsync(offered.Id.Value))!.IsTrainerHidden.Should().BeTrue();
        (await EntryOfAsync(offered.Id.Value))!.IsPublished.Should().BeTrue();
        (await EntryOfAsync(withdrawn.Id.Value))!.IsTrainerHidden.Should().BeTrue();
        (await EntryOfAsync(withdrawn.Id.Value))!.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Show trainer catalog async, after a hiding, puts back what was published and nothing else.
    /// </summary>
    /// <remarks>
    /// The whole argument for hiding rather than removing: the lifting costs one call and restores
    /// exactly what was on offer, never what was merely written (ADR 0056).
    /// </remarks>
    [Fact]
    public async Task ShowTrainerCatalogAsync_AfterAHiding_PutsBackWhatWasPublishedAndNothingElse()
    {
        var trainer = await GivenTrainerAsync();
        var offered = await GivenTrainingAsync(trainer, "First Offer");
        var withdrawn = await GivenTrainingAsync(trainer, "Second Offer", published: false);

        await Indexer.IndexAsync(offered.Id.Value, trainer.Id.Value);
        await Indexer.IndexAsync(withdrawn.Id.Value, trainer.Id.Value);

        await Indexer.HideTrainerCatalogAsync(trainer.Id.Value);
        await Indexer.ShowTrainerCatalogAsync(trainer.Id.Value);

        (await EntryOfAsync(offered.Id.Value))!.IsTrainerHidden.Should().BeFalse();
        (await EntryOfAsync(offered.Id.Value))!.IsPublished.Should().BeTrue();
        (await EntryOfAsync(withdrawn.Id.Value))!.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Hide trainer catalog async, a trainer the index never heard of, is not an error.
    /// </summary>
    [Fact]
    public async Task HideTrainerCatalogAsync_ATrainerTheIndexNeverHeardOf_IsNotAnError()
    {
        var hiding = async () => await Indexer.HideTrainerCatalogAsync(Guid.NewGuid());

        await hiding.Should().NotThrowAsync();
    }

    /// <summary>
    /// Index async, an empty identifier, writes nothing rather than throwing.
    /// </summary>
    /// <remarks>
    /// The typed identifiers refuse <see cref="Guid.Empty"/>, and this adapter runs after a commit
    /// rather than behind the validation pipeline that turns a malformed request into a <c>400</c>.
    /// The same trap <c>TrainingOwnerQuery</c> met, answered the same way.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IndexAsync_AnEmptyIdentifier_WritesNothingRatherThanThrowing(bool trainingIsEmpty)
    {
        var trainer = await GivenTrainerAsync();
        var training = await GivenTrainingAsync(trainer, "Well Formed");

        var indexing = async () => await Indexer.IndexAsync(
            trainingIsEmpty ? Guid.Empty : training.Id.Value,
            trainingIsEmpty ? trainer.Id.Value : Guid.Empty);

        await indexing.Should().NotThrowAsync();

        (await EntryOfAsync(training.Id.Value)).Should().BeNull();
    }
}
