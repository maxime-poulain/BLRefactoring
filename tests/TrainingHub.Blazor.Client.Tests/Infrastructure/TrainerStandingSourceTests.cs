using AwesomeAssertions;
using Moq;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// Where the front end learns that its caller is under sanction (ADR 0057).
/// </summary>
/// <remarks>
/// A read rather than a claim, and therefore a thing that can fail. What is pinned here is the
/// arithmetic around that read: it happens once and is shared, it can be redone on demand, and when
/// it fails the answer is "active" — because a banner raised by a failed read would accuse somebody
/// of a sanction they may not be under, while the opposite mistake costs a <c>403</c> the API
/// answers anyway.
/// </remarks>
public sealed class TrainerStandingSourceTests
{
    private const string Motive = "Repeated breaches of the content policy.";

    private readonly Mock<ITrainerClient> _trainerClient = new();

    /// <summary>
    /// Get async, an active trainer, answers no sanction and no reason.
    /// </summary>
    [Fact]
    public async Task GetAsync_AnActiveTrainer_AnswersNoSanctionAndNoReason()
    {
        Answering("Active", reason: null);

        var standing = await new TrainerStandingSource(_trainerClient.Object).GetAsync();

        standing.IsSuspended.Should().BeFalse();
        standing.Reason.Should().BeNull();
    }

    /// <summary>
    /// Get async, a suspended trainer, carries the reason as written.
    /// </summary>
    /// <remarks>
    /// Unedited on purpose: the banner shows it and the email carries it, and two texts describing
    /// one sanction is how a product ends up arguing with itself.
    /// </remarks>
    [Fact]
    public async Task GetAsync_ASuspendedTrainer_CarriesTheReasonAsWritten()
    {
        Answering("Suspended", Motive);

        var standing = await new TrainerStandingSource(_trainerClient.Object).GetAsync();

        standing.IsSuspended.Should().BeTrue();
        standing.Reason.Should().Be(Motive);
    }

    /// <summary>
    /// Get async, asked twice, reads once.
    /// </summary>
    /// <remarks>
    /// The reason this is a shared source rather than a read on each page. Three pages and a layout
    /// ask the same question about the same caller; asking the API four times to answer it would be
    /// the version that works and is wasteful, which is the version nobody notices.
    /// </remarks>
    [Fact]
    public async Task GetAsync_AskedTwice_ReadsOnce()
    {
        Answering("Active", reason: null);

        var source = new TrainerStandingSource(_trainerClient.Object);

        await source.GetAsync();
        await source.GetAsync();

        _trainerClient.Verify(client => client.GetCurrentAsync(), Times.Once);
    }

    /// <summary>
    /// Refresh async, a suspension decided since, reads again and says so.
    /// </summary>
    /// <remarks>
    /// The mechanism a bodiless <c>403</c> depends on. The refusal carries no document by design, so
    /// the answer to "why" is one read away on a surface the sanction deliberately leaves open — and
    /// the layout is told, because the banner does not belong to the page that met the refusal.
    /// </remarks>
    [Fact]
    public async Task RefreshAsync_ASuspensionDecidedSince_ReadsAgainAndSaysSo()
    {
        _trainerClient
            .SetupSequence(client => client.GetCurrentAsync())
            .ReturnsAsync(Profile("Active", reason: null))
            .ReturnsAsync(Profile("Suspended", Motive));

        var source = new TrainerStandingSource(_trainerClient.Object);
        var announced = 0;
        source.Changed += () => announced++;

        (await source.GetAsync()).IsSuspended.Should().BeFalse();

        var standing = await source.RefreshAsync();

        standing.IsSuspended.Should().BeTrue();
        standing.Reason.Should().Be(Motive);
        announced.Should().Be(1);

        // And the new answer is the one every later caller gets, rather than the cached first.
        (await source.GetAsync()).IsSuspended.Should().BeTrue();
    }

    /// <summary>
    /// Get async, the read failed, answers active rather than accusing anybody.
    /// </summary>
    /// <remarks>
    /// Two cases with one answer: an account that is nobody's trainer meets a <c>403</c> and has no
    /// standing on this surface at all, and an unreachable API means unknown. Neither is a sanction,
    /// and the generator's own sentence never reaches a screen.
    /// </remarks>
    [Fact]
    public async Task GetAsync_TheReadFailed_AnswersActiveRatherThanAccusingAnybody()
    {
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                "",
                new Dictionary<string, IEnumerable<string>>(),
                null));

        var standing = await new TrainerStandingSource(_trainerClient.Object).GetAsync();

        standing.IsSuspended.Should().BeFalse();
        standing.Reason.Should().BeNull();
    }

    private void Answering(string status, string? reason) =>
        _trainerClient.Setup(client => client.GetCurrentAsync()).ReturnsAsync(Profile(status, reason));

    private static SwaggerResponse<TrainerHttpResponse> Profile(string status, string? reason) =>
        new(
            200,
            new Dictionary<string, IEnumerable<string>>(),
            new TrainerHttpResponse
            {
                Id = Guid.NewGuid(),
                Firstname = "Ada",
                Lastname = "Lovelace",
                ContactEmail = "ada@example.com",
                Status = status,
                SuspensionReason = reason
            });
}
