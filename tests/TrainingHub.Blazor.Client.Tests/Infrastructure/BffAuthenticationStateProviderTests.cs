using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using TrainingHub.Blazor.Client.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// Behavior covered for the provider that asks the BFF who the user is.
/// </summary>
/// <remarks>
/// Identity used to be a JWT read out of local storage and parsed in the page. There is no token in
/// the browser any more, so everything this class does is about the one remaining question — what
/// to believe when <c>/bff/user</c> answers something other than a signed-in person. Two of those
/// answers are load-bearing and neither is obvious from reading the type: an unreachable BFF is
/// anonymous rather than an exception, and the answer is cached, because otherwise every
/// <c>AuthorizeView</c> on a page issues its own request. See ADR 0009.
/// </remarks>
public sealed class BffAuthenticationStateProviderTests
{
    /// <summary>
    /// Get authentication state, claims reported, signs the user in.
    /// </summary>
    [Fact]
    public async Task GetAuthenticationState_ClaimsReported_SignsTheUserIn()
    {
        // Arrange
        var provider = Given(Json("""
            {"claims":[{"type":"name","value":"john"},{"type":"role","value":"Trainer"}]}
            """));

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.Claims.Should().SatisfyRespectively(
            claim => claim.Should().Match<Claim>(c => c.Type == "name" && c.Value == "john"),
            claim => claim.Should().Match<Claim>(c => c.Type == "role" && c.Value == "Trainer"));
    }

    /// <summary>
    /// Get authentication state, no claims, is anonymous.
    /// </summary>
    /// <remarks>
    /// The BFF answers a document with an empty list rather than a 401 when nobody is signed in.
    /// Building a <c>ClaimsIdentity</c> with an authentication type and no claims would report an
    /// authenticated user with no name, which is the one answer that is worse than either.
    /// </remarks>
    [Fact]
    public async Task GetAuthenticationState_NoClaims_IsAnonymous()
    {
        // Arrange
        var provider = Given(Json("""{"claims":[]}"""));

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    /// <summary>
    /// Get authentication state, BFF unreachable, is anonymous rather than throwing.
    /// </summary>
    /// <remarks>
    /// An exception here is not caught by a page: it escapes into the renderer while the layout is
    /// deciding what to show, and the application renders half of itself. Being unreachable is not
    /// an identity, so the signed-out interface is the honest answer.
    /// </remarks>
    [Fact]
    public async Task GetAuthenticationState_BffUnreachable_IsAnonymousRatherThanThrowing()
    {
        // Arrange
        var provider = new BffAuthenticationStateProvider(
            new StubHttpClientFactory(_ => throw new HttpRequestException("no route to host")));

        // Act
        Func<Task<AuthenticationState>> act = provider.GetAuthenticationStateAsync;

        // Assert
        var state = await act.Should().NotThrowAsync();
        state.Subject.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    /// <summary>
    /// Get authentication state, read twice, asks the BFF once.
    /// </summary>
    /// <remarks>
    /// The cache is the whole reason this class holds a field. Losing it costs one request per
    /// <c>AuthorizeView</c> per render, and nothing about the interface would look wrong.
    /// </remarks>
    [Fact]
    public async Task GetAuthenticationState_ReadTwice_AsksTheBffOnce()
    {
        // Arrange
        var factory = new StubHttpClientFactory(_ => Json("""{"claims":[{"type":"name","value":"john"}]}"""));
        var provider = new BffAuthenticationStateProvider(factory);

        // Act
        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();

        // Assert
        factory.Requests.Should().ContainSingle()
            .Which.RequestUri!.AbsolutePath.Should().Be("/bff/user");
    }

    /// <summary>
    /// Notify authentication changed, re-reads the cookie's answer and announces it.
    /// </summary>
    /// <remarks>
    /// It re-fetches rather than trusting what the caller believes happened, because the cookie is
    /// the truth and this host is the only thing that can read it. A sign-out that merely cleared a
    /// cached principal would leave the interface signed out and the session alive.
    /// </remarks>
    [Fact]
    public async Task NotifyAuthenticationChanged_ReReadsAndAnnouncesIt()
    {
        // Arrange
        var signedIn = true;

        var factory = new StubHttpClientFactory(_ => signedIn
            ? Json("""{"claims":[{"type":"name","value":"john"}]}""")
            : Json("""{"claims":[]}"""));

        var provider = new BffAuthenticationStateProvider(factory);
        await provider.GetAuthenticationStateAsync();

        var announced = new List<bool>();
        provider.AuthenticationStateChanged += task => announced.Add(Resolve(task));

        // Act
        signedIn = false;
        provider.NotifyAuthenticationChanged();

        // Assert
        announced.Should().Equal(false);
        (await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
        factory.Requests.Should().HaveCount(2);
    }

    /// <summary>
    /// Get authentication state, asks the client pointed at the BFF.
    /// </summary>
    [Fact]
    public async Task GetAuthenticationState_AsksTheClientPointedAtTheBff()
    {
        // Arrange
        var factory = new StubHttpClientFactory(_ => Json("""{"claims":[]}"""));
        var provider = new BffAuthenticationStateProvider(factory);

        // Act
        await provider.GetAuthenticationStateAsync();

        // Assert
        factory.ClientNames.Should().Equal(HttpClientNames.Bff);
    }

    private static bool Resolve(Task<AuthenticationState> task) =>
        task.GetAwaiter().GetResult().User.Identity!.IsAuthenticated;

    private static BffAuthenticationStateProvider Given(HttpResponseMessage response) =>
        new(new StubHttpClientFactory(_ => response));

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
}
