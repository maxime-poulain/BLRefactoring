using AwesomeAssertions;
using Moq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Queries;

/// <summary>
/// The one read of the trainer's contact address, proven against the write model it projects from.
/// </summary>
/// <remarks>
/// What this adapter answers is what the delivery consumer sends to, so the claim worth a database
/// is the projection itself: the address is <c>Trainer.ContactEmail</c> — the business attribute a
/// trainer publishes for being written to — which lives in <c>TrainingContext</c>, where the
/// account's credential never has (ADR 0056, ADR 0082). The account address cannot be read here by
/// construction, because the Identity store is another context entirely; what remains to prove is
/// that the right column comes back, through the value conversion, beside the right names.
/// Against SQLite rather than a substitute, for the reason every adapter suite here gives: the
/// risk is the translation (ADR 0032).
/// </remarks>
public sealed class TrainerContactQueryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Mock<IAccountLanguageQuery> _languages = new();
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
    /// Get by trainer id async, a known trainer, answers the published address and the name.
    /// </summary>
    /// <remarks>
    /// The three fields the delivery consumer composes with: where the message goes, and who to
    /// greet. The address asserted is the one the builder published, spelled in full, because a
    /// projection that answered the local part or a reformatted value would produce mail that
    /// bounces rather than a test that fails.
    /// </remarks>
    [Fact]
    public async Task GetByTrainerIdAsync_AKnownTrainer_AnswersThePublishedAddressAndTheName()
    {
        var ada = Trainer("Ada", "Lovelace", "ada.published@example.com");

        await AddAsync(ada);

        _languages
            .Setup(languages => languages.ByTrainerIdAsync(ada.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ru");

        var contact = await Sut().GetByTrainerIdAsync(ada.Id.Value);

        contact.Should().NotBeNull();
        contact!.EmailAddress.Should().Be("ada.published@example.com");
        contact.Firstname.Should().Be("Ada");
        contact.Lastname.Should().Be("Lovelace");
        contact.Language.Should().Be(
            "ru",
            "the language rides beside the address, asked of the port rather than joined — this " +
            "adapter never opens the Identity store (ADR 0082, ADR 0091)");
    }

    /// <summary>
    /// Get by trainer id async, an identifier nobody answers to, answers nothing.
    /// </summary>
    /// <remarks>
    /// The consumer reads this as "log the identifier and stop": a trainer deleted between the
    /// visitor's click and the delivery is not a transient failure, and an exception here would
    /// spend the consumer's whole retry budget re-reading the same absence (ADR 0033).
    /// </remarks>
    [Fact]
    public async Task GetByTrainerIdAsync_AnIdentifierNobodyAnswersTo_AnswersNothing()
    {
        await AddAsync(Trainer("Ada", "Lovelace", "ada.published@example.com"));

        var contact = await Sut().GetByTrainerIdAsync(Guid.NewGuid());

        contact.Should().BeNull();
    }

    private TrainerContactQuery Sut() => new(_context, _languages.Object);

    private static Trainer Trainer(string firstname, string lastname, string contactEmail) =>
        new TrainerBuilder()
            .WithFirstname(firstname)
            .WithLastname(lastname)
            .WithContactEmail(contactEmail)
            .Build();

    private async Task AddAsync(params Trainer[] trainers)
    {
        _context.Trainers.AddRange(trainers);
        await _context.SaveChangesAsync();
    }
}
