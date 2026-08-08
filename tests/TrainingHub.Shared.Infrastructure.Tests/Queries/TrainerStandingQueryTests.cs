using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Queries;

/// <summary>
/// The one bit every write of the trainer surface is held to (ADR 0053).
/// </summary>
/// <remarks>
/// This port is read on the authorization path, before any use case runs, so its edges are the ones
/// no request can reach: an identifier naming nobody, and the empty identifier. Both answer rather
/// than throw, and both would otherwise be discovered as a <c>500</c> out of the authorization
/// stage — the failure mode <c>TrainingOwnerQuery</c> met first, on the same path.
/// <para>
/// Against SQLite rather than a substitute, because the comparison is against a value-converted
/// identifier and a closed-set status: a fake would answer the same question in LINQ to objects and
/// prove nothing about translating either.
/// </para>
/// </remarks>
public sealed class TrainerStandingQueryTests : IAsyncLifetime
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
    /// Is suspended async, an active trainer, answers no.
    /// </summary>
    [Fact]
    public async Task IsSuspendedAsync_AnActiveTrainer_AnswersNo()
    {
        var ada = await AddAsync(Trainer());

        (await new TrainerStandingQuery(_context).IsSuspendedAsync(ada.Id.Value))
            .Should().BeFalse();
    }

    /// <summary>
    /// Is suspended async, a suspended trainer, answers yes.
    /// </summary>
    /// <remarks>
    /// The whole point of the port, and the assertion that would fail if the status were compared
    /// as a string rather than as the closed set the domain declares.
    /// </remarks>
    [Fact]
    public async Task IsSuspendedAsync_ASuspendedTrainer_AnswersYes()
    {
        var ada = Trainer();
        ada.Suspend(SuspensionReason.Create("Repeated breaches.").ShouldBeSuccess()).ShouldBeSuccess();

        await AddAsync(ada);

        (await new TrainerStandingQuery(_context).IsSuspendedAsync(ada.Id.Value))
            .Should().BeTrue();
    }

    /// <summary>
    /// Is suspended async, an identifier nobody answers to, answers no rather than throwing.
    /// </summary>
    /// <remarks>
    /// The policy's job is to refuse a sanctioned caller, not to prove a caller exists. Refusing
    /// here would turn "your trainer record is gone" into "you are under sanction", and would tell
    /// the caller something false about their permissions where a <c>404</c> from the action is the
    /// honest answer.
    /// </remarks>
    [Fact]
    public async Task IsSuspendedAsync_AnIdentifierNobodyAnswersTo_AnswersNo()
    {
        (await new TrainerStandingQuery(_context).IsSuspendedAsync(Guid.NewGuid()))
            .Should().BeFalse();
    }

    /// <summary>
    /// Is suspended async, the empty identifier, answers no without asking the database.
    /// </summary>
    /// <remarks>
    /// Two failures in one assertion, separated by the unconnected context. With the guard gone,
    /// <c>TrainerId.Create</c> refuses <see cref="Guid.Empty"/> and this throws out of the
    /// authorization stage — a <c>500</c> for a request the validation pipeline was standing by to
    /// refuse with a <c>400</c>. With the guard written after the query instead of before it, this
    /// opens a connection nothing answers. Only answering immediately passes.
    /// </remarks>
    [Fact]
    public async Task IsSuspendedAsync_TheEmptyIdentifier_AnswersNoWithoutAskingTheDatabase()
    {
        await using var unconnected = new TrainingContext(
            new DbContextOptionsBuilder<TrainingContext>()
                // A syntactically valid connection string nothing will ever open.
                .UseSqlServer("Server=unreachable;Database=unused;Integrated Security=false;User Id=nobody;Password=nothing;TrustServerCertificate=true")
                .Options);

        (await new TrainerStandingQuery(unconnected).IsSuspendedAsync(Guid.Empty))
            .Should().BeFalse();
    }

    private static Trainer Trainer() =>
        new TrainerBuilder()
            .WithFirstname("Ada")
            .WithLastname("Lovelace")
            .WithContactEmail("ada.lovelace@example.com")
            .Build();

    private async Task<Trainer> AddAsync(Trainer trainer)
    {
        _context.Trainers.Add(trainer);
        await _context.SaveChangesAsync();

        return trainer;
    }
}
