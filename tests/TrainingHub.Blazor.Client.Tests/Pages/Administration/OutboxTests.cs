using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Pages.Administration;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Administration;

/// <summary>
/// Behavior covered for the outbox's operator page.
/// </summary>
/// <remarks>
/// The screen an operator reaches when something has already gone wrong, so what it shows matters
/// as much as what it sends: the consumers a retry will skip are the difference between pressing the
/// button and sending a second welcome email, and a page that omitted them would make a safe
/// operation look like a gamble (ADR 0034, ADR 0061).
/// </remarks>
public sealed class OutboxTests : ComponentTest
{
    private readonly Mock<IOutboxClient> _outbox = new();

    /// <summary>Outbox tests.</summary>
    public OutboxTests()
    {
        Services.AddSingleton(_outbox.Object);

        _outbox
            .Setup(client => client.GetPoisonAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 0));
    }

    /// <summary>
    /// Renders, asks the server for the first page and lets it choose the size.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheFirstPage_AndLetsItChooseTheSize()
    {
        Render<Outbox>();

        _outbox.Verify(client => client.GetPoisonAsync(1, null), Times.Once);
    }

    /// <summary>
    /// Renders, nothing poisoned, says so as reassurance rather than as an absence of matches.
    /// </summary>
    /// <remarks>
    /// The one listing in this front end whose empty state is the good one, which is why it does not
    /// borrow the "no match" wording every other page uses.
    /// </remarks>
    [Fact]
    public void Renders_NothingPoisoned_SaysSoAsReassurance()
    {
        var page = Render<Outbox>();

        page.Markup.Should().Contain("Nothing was given up on");
    }

    /// <summary>
    /// Renders, a poisoned message, names the consumers a retry would skip.
    /// </summary>
    [Fact]
    public void Renders_APoisonedMessage_NamesTheConsumersARetryWouldSkip()
    {
        _outbox
            .Setup(client => client.GetPoisonAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 1, Poisoned(deliveredTo: ["WelcomeEmail"])));

        var page = Render<Outbox>();

        page.Markup.Should().Contain("WelcomeEmail");
        page.Markup.Should().Contain("TrainerCreated");
    }

    /// <summary>
    /// Requeue, the row it was pressed on, asks the server for that message.
    /// </summary>
    [Fact]
    public void Requeue_TheRowItWasPressedOn_AsksTheServerForThatMessage()
    {
        var message = Poisoned(deliveredTo: []);

        _outbox
            .Setup(client => client.GetPoisonAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 1, message));

        var page = Render<Outbox>();

        page.FindAll("button").Single(button => button.TextContent.Contains("Requeue", StringComparison.Ordinal)).Click();

        _outbox.Verify(client => client.RequeueAsync(message.Id), Times.Once);
    }

    private static PoisonedMessageHttpResponse Poisoned(List<string> deliveredTo) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "TrainerCreated",
        Version = 1,
        OccurredOnUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
        Attempts = 5,
        Error = "System.InvalidOperationException: nobody could read it",
        DeliveredConsumers = deliveredTo
    };

    private static PagedHttpResponseOfPoisonedMessageHttpResponse Page(
        int totalCount,
        params PoisonedMessageHttpResponse[] items) => new()
        {
            Items = [.. items],
            Page = 1,
            PageSize = 20,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : 1,
            HasNextPage = false,
            HasPreviousPage = false
        };
}
