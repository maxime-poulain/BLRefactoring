using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Repositories;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Repositories;

/// <summary>
/// What the training repository asks a database about a title, asked against one.
/// </summary>
/// <remarks>
/// Two questions land here, and neither had anything fast watching it.
/// <c>TrainingTitleExistsForTrainerSpecification</c> is exercised in the domain suite through its
/// compiled delegate, and every other caller reaches <see cref="IUniquenessTitleChecker"/> through a
/// substitute — so the only thing that ever put its criteria through a query translator was the SQL
/// Server integration suite, which the fast workflow excludes. The administrative term is newer and
/// even more dependent on translation: it exists at all because ADR 0060 made the title a complex
/// property, and it is one mapping change away from silently ceasing to translate.
/// <para>
/// Against SQLite rather than the in-memory provider, for the reason <c>TrainerNamesQueryTests</c>
/// gives: the in-memory provider answers in LINQ to objects, which is precisely the evaluation this
/// suite exists not to trust.
/// </para>
/// </remarks>
public sealed class TitleUniquenessTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private TrainingContext _context = null!;

    /// <summary>Opens the connection the database lives inside, and builds the schema on it.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingContext(new DbContextOptionsBuilder<TrainingContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
            .Options);

        await _context.Database.EnsureCreatedAsync();
    }

    /// <summary>Closes the context and, with the connection, the database itself.</summary>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Title for trainer exists async, a title that trainer already claims, answers true.
    /// </summary>
    /// <remarks>
    /// The whole point is that this runs as SQL. An answer computed in memory would agree with a
    /// criteria the database cannot express, which is the failure this suite is here to see.
    /// </remarks>
    [Fact]
    public async Task TitleForTrainerExistsAsync_ATitleThatTrainerAlreadyClaims_AnswersTrue()
    {
        var trainer = await GivenTrainerAsync();
        await GivenTrainingAsync(trainer, "Domain Driven Design");

        var exists = await Checker().TitleForTrainerExistsAsync(Title("Domain Driven Design"), trainer.Id);

        exists.Should().BeTrue();
    }

    /// <summary>
    /// Title for trainer exists async, the same title under another trainer, answers false.
    /// </summary>
    /// <remarks>
    /// The rule is scoped to a trainer, not global — the same scoping the unique index carries on
    /// <c>(TrainerId, Title)</c>. A criteria that dropped the owner would answer true here and refuse
    /// a title nobody had claimed.
    /// </remarks>
    [Fact]
    public async Task TitleForTrainerExistsAsync_TheSameTitleUnderAnotherTrainer_AnswersFalse()
    {
        var owner = await GivenTrainerAsync();
        await GivenTrainingAsync(owner, "Domain Driven Design");

        var somebodyElse = await GivenTrainerAsync();

        var exists = await Checker().TitleForTrainerExistsAsync(Title("Domain Driven Design"), somebodyElse.Id);

        exists.Should().BeFalse();
    }

    /// <summary>
    /// Title for trainer exists async, a title that trainer does not claim, answers false.
    /// </summary>
    [Fact]
    public async Task TitleForTrainerExistsAsync_ATitleThatTrainerDoesNotClaim_AnswersFalse()
    {
        var trainer = await GivenTrainerAsync();
        await GivenTrainingAsync(trainer, "Domain Driven Design");

        var exists = await Checker().TitleForTrainerExistsAsync(Title("Event Storming"), trainer.Id);

        exists.Should().BeFalse();
    }

    /// <summary>
    /// Get page async, a term, matches the title as a substring.
    /// </summary>
    /// <remarks>
    /// The fact ADR 0060 exists for, and it only holds as SQL. Against the converted column this
    /// query answered "could not be translated"; against the complex property it runs. A middle
    /// word is used rather than a leading one, because a prefix would also pass against an index
    /// this listing deliberately does not use.
    /// </remarks>
    [Fact]
    public async Task GetPageAsync_ATerm_MatchesTheTitleAsASubstring()
    {
        var trainer = await GivenTrainerAsync();
        await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenTrainingAsync(trainer, "Event Storming");

        var page = await Checker().GetPageAsync(status: null, "Driven", new PageRequest());

        page.Items.Should().ContainSingle()
            .Which.Title.Value.Should().Be("Domain Driven Design");
        page.TotalCount.Should().Be(1);
    }

    /// <summary>
    /// Get page async, a term and a state, composes them rather than replacing one.
    /// </summary>
    /// <remarks>
    /// Two criteria that replaced one another would answer plausibly for every single-criterion
    /// call and be wrong for exactly the question a moderator asks — <em>the withheld one called
    /// this</em>.
    /// </remarks>
    [Fact]
    public async Task GetPageAsync_ATermAndAState_ComposesThemRatherThanReplacingOne()
    {
        var trainer = await GivenTrainerAsync();
        var withheld = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenTrainingAsync(trainer, "Domain Modeling");

        withheld.Withhold(WithholdingReason.Create("Plagiarised material.").ShouldBeSuccess())
            .ShouldBeSuccess();
        await _context.SaveChangesAsync();

        var page = await Checker().GetPageAsync(TrainingStatus.Withheld, "Domain", new PageRequest());

        page.Items.Should().ContainSingle()
            .Which.Title.Value.Should().Be("Domain Driven Design");
    }

    /// <summary>
    /// Get page async, a blank term, narrows nothing.
    /// </summary>
    /// <remarks>
    /// A blank term is no term, the reading the trainers' question already gives it — and the
    /// method is total on its own rather than trusting the boundary to have trimmed.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetPageAsync_ABlankTerm_NarrowsNothing(string? term)
    {
        var trainer = await GivenTrainerAsync();
        await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenTrainingAsync(trainer, "Event Storming");

        var page = await Checker().GetPageAsync(status: null, term, new PageRequest());

        page.TotalCount.Should().Be(2);
    }

    private TrainingRepository Checker() => new(_context);

    private static TrainingTitle Title(string value) => TrainingTitle.Create(value).ShouldBeSuccess();

    private async Task<Trainer> GivenTrainerAsync()
    {
        var trainer = new TrainerBuilder()
            .WithFirstname("Ada")
            .WithLastname("Lovelace")
            .WithContactEmail($"ada.{Guid.NewGuid():N}@example.com")
            .Build();

        _context.Trainers.Add(trainer);
        await _context.SaveChangesAsync();

        return trainer;
    }

    private async Task<Training> GivenTrainingAsync(Trainer owner, string title)
    {
        var training = (await new TrainingBuilder()
            .WithTitle(title)
            .WithTrainerId(owner.Id.Value)
            .BuildAsync()).ShouldBeSuccess();

        _context.Trainings.Add(training);
        await _context.SaveChangesAsync();

        return training;
    }
}
