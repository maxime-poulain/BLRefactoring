using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Moq;
using TrainingHub.Blazor.Client.Authorization;
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

        var standing = await Source().GetAsync();

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

        var standing = await Source().GetAsync();

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

        var source = Source();

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

        var source = Source();
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

        var standing = await Source().GetAsync();

        standing.IsSuspended.Should().BeFalse();
        standing.Reason.Should().BeNull();
    }

    /// <summary>
    /// Find own portrait async, a trainer with a photo, answers the authenticated address.
    /// </summary>
    /// <remarks>
    /// The authenticated route with the photo's identity as a cache buster — the same shape the
    /// profile page builds (ADR 0063), and never the public portrait route, which answers 404 for
    /// a suspended trainer who is still entitled to their own face.
    /// </remarks>
    [Fact]
    public async Task FindOwnPortraitAsync_ATrainerWithAPhoto_AnswersTheAuthenticatedAddress()
    {
        var trainerId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        Answering("Active", reason: null, trainerId, photoId);

        var portrait = await Source().FindOwnPortraitAsync();

        portrait.Should().Be($"api/Trainer/{trainerId}/photo?v={photoId}");
    }

    /// <summary>
    /// Find own portrait async, no photo, answers nothing rather than a placeholder.
    /// </summary>
    [Fact]
    public async Task FindOwnPortraitAsync_NoPhoto_AnswersNothingRatherThanAPlaceholder()
    {
        Answering("Active", reason: null);

        var portrait = await Source().FindOwnPortraitAsync();

        portrait.Should().BeNull("no photo renders as initials, never as a silhouette (ADR 0074)");
    }

    /// <summary>
    /// Find own portrait async, the read failed, answers nothing.
    /// </summary>
    /// <remarks>
    /// The same two cases as the standing's fallback: an administrator's account meets a refusal
    /// and has no portrait on this surface at all, and an unreachable API means unknown.
    /// </remarks>
    [Fact]
    public async Task FindOwnPortraitAsync_TheReadFailed_AnswersNothing()
    {
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (403).",
                403,
                "",
                new Dictionary<string, IEnumerable<string>>(),
                null));

        var portrait = await Source().FindOwnPortraitAsync();

        portrait.Should().BeNull();
    }

    /// <summary>
    /// The standing and the portrait, share one read.
    /// </summary>
    /// <remarks>
    /// The reason the portrait source is a second face of this class rather than a class of its
    /// own: both answers live in the same document, and the layout asking the API twice for it
    /// would be the version that works and is wasteful — the version nobody notices.
    /// </remarks>
    [Fact]
    public async Task TheStandingAndThePortrait_ShareOneRead()
    {
        Answering("Active", reason: null);

        var source = Source();

        await source.GetAsync();
        await source.FindOwnPortraitAsync();

        _trainerClient.Verify(client => client.GetCurrentAsync(), Times.Once);
    }

    /// <summary>
    /// Get async, an account that is nobody's trainer, asks the API nothing.
    /// </summary>
    /// <remarks>
    /// The refusal used to be met rather than avoided: an administrator spent one <c>403</c> per
    /// scope being told what their own identity already said, and the banner rendered from a
    /// failure. Asking the claim first is what makes the call unnecessary — and the standing it
    /// answers is active, because a caller with no standing is under no sanction (ADR 0078).
    /// </remarks>
    [Fact]
    public async Task GetAsync_AnAccountThatIsNobodysTrainer_AsksTheApiNothing()
    {
        var standing = await Source(isTrainer: false).GetAsync();

        standing.IsSuspended.Should().BeFalse();
        _trainerClient.Verify(client => client.GetCurrentAsync(), Times.Never);
    }

    /// <summary>
    /// Find own portrait async, an account that is nobody's trainer, asks the API nothing.
    /// </summary>
    [Fact]
    public async Task FindOwnPortraitAsync_AnAccountThatIsNobodysTrainer_AsksTheApiNothing()
    {
        var portrait = await Source(isTrainer: false).FindOwnPortraitAsync();

        portrait.Should().BeNull();
        _trainerClient.Verify(client => client.GetCurrentAsync(), Times.Never);
    }

    private TrainerStandingSource Source(bool isTrainer = true) =>
        new(_trainerClient.Object, new StubAuthenticationStateProvider(isTrainer));

    /// <summary>
    /// The identity the source reads before deciding whether the call is worth making.
    /// </summary>
    /// <remarks>
    /// A trainer's identity carries the claim the API's own policy demands; an administrator's
    /// carries the role and nothing else, which is exactly what <c>TokenService</c> mints.
    /// </remarks>
    private sealed class StubAuthenticationStateProvider(bool isTrainer) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                isTrainer ? [new Claim(SessionClaims.TrainerId, Guid.NewGuid().ToString())] : [],
                authenticationType: "test"))));
    }

    private void Answering(string status, string? reason, Guid? trainerId = null, Guid? photoId = null) =>
        _trainerClient.Setup(client => client.GetCurrentAsync())
            .ReturnsAsync(Profile(status, reason, trainerId, photoId));

    private static SwaggerResponse<TrainerHttpResponse> Profile(
        string status, string? reason, Guid? trainerId = null, Guid? photoId = null) =>
        new(
            200,
            new Dictionary<string, IEnumerable<string>>(),
            new TrainerHttpResponse
            {
                Id = trainerId ?? Guid.NewGuid(),
                Firstname = "Ada",
                Lastname = "Lovelace",
                ContactEmail = "ada@example.com",
                Status = status,
                SuspensionReason = reason,
                PhotoId = photoId
            });

    /// <summary>
    /// Get async, the BFF was unreachable, answers active rather than throwing.
    /// </summary>
    /// <remarks>
    /// The transport failure arrives as <see cref="HttpRequestException"/> rather than
    /// <see cref="ApiException"/>, and every caller of this source is a page initializer that a
    /// throw would kill outright.
    /// </remarks>
    [Fact]
    public async Task GetAsync_TheBffWasUnreachable_AnswersActiveRatherThanThrowing()
    {
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ThrowsAsync(new HttpRequestException("no route to host"));

        var standing = await Source().GetAsync();

        standing.IsSuspended.Should().BeFalse();
    }
}
