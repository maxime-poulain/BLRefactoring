using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace TrainingHub.Blazor.Bff.Tests;

/// <summary>
/// What ADR 0009 claims, asserted.
/// </summary>
/// <remarks>
/// Every claim in that record is about something the code cannot show on its own: that the token
/// reaches the API and not the browser, that a forged request is refused, that signing out ends the
/// session rather than merely forgetting it. Each of those was a paragraph asking to be believed.
/// <para>
/// A host per test rather than a shared fixture. There is no database here and startup is cheap,
/// and the tests assert on what the API was *not* sent — which is only meaningful when nothing
/// else has talked to it.
/// </para>
/// </remarks>
public sealed class BffTests : IDisposable
{
    private const string LoginPath = "bff/login";
    private const string UserPath = "bff/user";
    private const string LogoutPath = "bff/logout";
    private const string ForwardedPath = "api/Trainer/me";
    private const string AnonymousPath = "api/Catalog/trainings";

    private readonly BffFactory _factory = new();
    private readonly HttpClient _browser;
    private readonly string _token = BffFactory.IssueToken();

    /// <summary>
    /// Bff tests.
    /// </summary>
    public BffTests()
    {
        _browser = _factory.CreateBrowser();
        _factory.LoginApi.Respond = _ => RecordingHandler.Ok($$"""{"token":"{{_token}}"}""");
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        _browser.Dispose();
        _factory.Dispose();
    }

    // ---------------------------------------------------------------- the page itself

    /// <summary>
    /// The application is still served.
    /// </summary>
    [Fact]
    public async Task The_application_is_still_served()
    {
        var response = await _browser.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the host stopped registering the browser's services — those describe the other side " +
            "of this one, and registering them collided with its own API client");
    }

    /// <summary>
    /// Liveness answers healthy to an anonymous caller.
    /// </summary>
    [Fact]
    public async Task Liveness_answers_healthy_to_an_anonymous_caller()
    {
        var response = await _browser.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "every host answers for its own health, and the BFF's share is the framework's " +
            "liveness pair inline (ADR 0037)");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    // ---------------------------------------------------------------- signing in

    /// <summary>
    /// Login answers a cookie and never the token.
    /// </summary>
    [Fact]
    public async Task Login_answers_a_cookie_and_never_the_token()
    {
        var response = await SignInAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(_token, "the token is the one thing that must not reach the browser");

        var cookie = response.Headers.GetValues("Set-Cookie").Single();

        cookie.Should().StartWith("__Host-bff=");
        cookie.Should().ContainEquivalentOf("httponly", "script must not be able to read it — the whole point");
        cookie.Should().ContainEquivalentOf("secure");
        cookie.Should().ContainEquivalentOf("samesite=strict", "nothing here expects to arrive from another site");
        cookie.Should().NotContain(_token, "the ticket is encrypted, not a wrapper around the token");
    }

    /// <summary>
    /// Login writes a session cookie that carries the tokens own expiry.
    /// </summary>
    [Fact]
    public async Task Login_writes_a_session_cookie_that_carries_the_tokens_own_expiry()
    {
        var response = await SignInAsync();

        var cookie = response.Headers.GetValues("Set-Cookie").Single();

        // No `expires` attribute, deliberately: IsPersistent is false, so the browser drops the
        // cookie when it closes. The expiry that matters is inside the encrypted ticket —
        // ExpiresUtc, taken from the JWT's own `exp` — and the handler refuses the ticket past it.
        // Which is why the assertion below is about configuration rather than about this header:
        // nothing in a Set-Cookie can show whether the ticket will be renewed.
        cookie.Should().NotContainEquivalentOf("expires=");
        cookie.Should().NotContainEquivalentOf("max-age=");
    }

    /// <summary>
    /// The cookie session is never extended past the token.
    /// </summary>
    [Fact]
    public void The_cookie_session_is_never_extended_past_the_token()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // ADR 0009 says the cookie dies with the token it carries, and half of that sentence had no
        // line behind it. Sliding expiration defaults to ON: the handler renews the ticket once past
        // half its window and grants a fresh full window each time, so the session outlives the JWT
        // and the user stays apparently signed in while every forwarded call comes back 401. There
        // is no refresh token to reach for — the API issues one credential, with one lifetime.
        //
        // Asserted against the configured options because the behavior is not observable from
        // outside without waiting out half a token's life, and a test that sleeps is a test nobody
        // keeps. What it pins is exactly the line that was missing.
        options.SlidingExpiration.Should().BeFalse(
            "a renewed ticket outlives the token it was cut from");
    }

    /// <summary>
    /// Login without the application header never reaches the api.
    /// </summary>
    [Fact]
    public async Task Login_without_the_application_header_never_reaches_the_api()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginPath)
        {
            Content = Credentials()
        };

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.LoginApi.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// Login passes the apis refusal through.
    /// </summary>
    [Fact]
    public async Task Login_passes_the_apis_refusal_through()
    {
        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var response = await SignInAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------- identity

    /// <summary>
    /// User is anonymous before signing in.
    /// </summary>
    [Fact]
    public async Task User_is_anonymous_before_signing_in()
    {
        var response = await SendAsync(HttpMethod.Get, UserPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<BffUser>();

        user!.Claims.Should().BeEmpty("a signed-out caller is a document with no claims — never a " +
            "null one, which the framework writes as an empty body that does not parse");
    }

    /// <summary>
    /// User reports the canonical name claim after signing in.
    /// </summary>
    [Fact]
    public async Task User_reports_the_canonical_name_claim_after_signing_in()
    {
        await SignInAsync();

        var response = await SendAsync(HttpMethod.Get, UserPath);
        var user = await response.Content.ReadFromJsonAsync<BffUser>();

        user!.Claims.Should().Contain(
            claim => claim.Type == ClaimTypes.Name && claim.Value == "alice",
            "the JWT carries it as `unique_name`, and an interface asking for User.Identity.Name " +
            "would otherwise render nothing");
    }

    /// <summary>
    /// User without the application header is refused.
    /// </summary>
    [Fact]
    public async Task User_without_the_application_header_is_refused()
    {
        var response = await _browser.GetAsync(UserPath);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------- forwarding

    /// <summary>
    /// A forwarded call carries the token and drops the prefix.
    /// </summary>
    [Fact]
    public async Task A_forwarded_call_carries_the_token_and_drops_the_prefix()
    {
        await SignInAsync();

        var response = await SendAsync(HttpMethod.Get, ForwardedPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var forwarded = _factory.ProxiedApi.Requests.Should().ContainSingle().Subject;

        forwarded.Uri!.AbsolutePath.Should().Be("/Trainer/me");
        forwarded.Authorization.Should().Be($"Bearer {_token}",
            "the credential is attached server-side, from the cookie");
    }

    /// <summary>
    /// A forwarded call without a session never reaches the api.
    /// </summary>
    /// <remarks>
    /// The reason narrowed when the catalog became reachable: it is no longer true of every path
    /// that the API would refuse an anonymous call anyway. It is still true of this one, and of
    /// everything the catch-all route matches, which is what this fact pins (ADR 0062).
    /// </remarks>
    [Fact]
    public async Task A_forwarded_call_without_a_session_never_reaches_the_api()
    {
        var response = await SendAsync(HttpMethod.Get, ForwardedPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "outside the catalog an anonymous call would be forwarded without a token and " +
            "refused anyway, one hop later");
        _factory.ProxiedApi.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// The catalog is forwarded without a session, and without a token.
    /// </summary>
    /// <remarks>
    /// The one path this proxy carries for a visitor who has never signed in. Two assertions rather
    /// than one: that it arrives at all, and that it arrives <em>bare</em> — the token transform is
    /// untouched and simply finds nothing in the cookie, so an anonymous read cannot borrow anybody
    /// else's credential (ADR 0062).
    /// </remarks>
    [Fact]
    public async Task The_catalog_is_forwarded_without_a_session_and_without_a_token()
    {
        var response = await SendAsync(HttpMethod.Get, AnonymousPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var forwarded = _factory.ProxiedApi.Requests.Should().ContainSingle().Subject;

        forwarded.Uri!.AbsolutePath.Should().Be("/Catalog/trainings",
            "the prefix is dropped on this route as on the other one");
        forwarded.Authorization.Should().BeNull(
            "there is no session to take a token from, and the transform adds nothing rather than failing");
    }

    /// <summary>
    /// The catalog carries the token of a visitor who has one.
    /// </summary>
    /// <remarks>
    /// Anonymous means "no policy to satisfy", not "stripped". A signed-in visitor browsing the
    /// public catalog reaches the API as themselves, which is what keeps one route from behaving
    /// like two different proxies.
    /// </remarks>
    [Fact]
    public async Task The_catalog_carries_the_token_of_a_visitor_who_has_one()
    {
        await SignInAsync();

        var response = await SendAsync(HttpMethod.Get, AnonymousPath);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.ProxiedApi.Requests.Should().ContainSingle()
            .Which.Authorization.Should().Be($"Bearer {_token}");
    }

    /// <summary>
    /// A call that says nothing about where it came from is refused.
    /// </summary>
    /// <remarks>
    /// The safety argument of the anonymous route. Opening a path to anonymous callers does not
    /// open the proxy: the guard runs on the prefix, before authentication, and knows nothing about
    /// routes.
    /// <para>
    /// This request carries neither the application's header nor the browser's own account of its
    /// origin, so it proves nothing and is refused. A browser too old to send <c>Sec-Fetch-*</c>
    /// lands here: it loses images, not function (ADR 0063).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_call_that_says_nothing_about_where_it_came_from_is_refused()
    {
        var response = await _browser.GetAsync(AnonymousPath);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.ProxiedApi.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// An image this application asked for reaches the API without the application header.
    /// </summary>
    /// <remarks>
    /// The defect ADR 0063 found, from the other side. An <c>&lt;img&gt;</c> cannot set a custom
    /// header — which the guard's own documentation gives as its strength, and which is equally true
    /// of <em>our</em> images. Every portrait this front end rendered was refused 403 before
    /// reaching the API, and nothing noticed because nothing renders a real page against a real BFF.
    /// <para>
    /// What replaces the header for a safe read is the browser's own account of the origin.
    /// <c>Sec-Fetch-Site</c> is set by the browser and its name is forbidden to script, so a page
    /// cannot claim <c>same-origin</c> for a request it did not make from here.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_image_this_application_asked_for_reaches_the_api_without_the_application_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AnonymousPath);

        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        request.Headers.Add("Sec-Fetch-Dest", "image");

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.ProxiedApi.Requests.Should().ContainSingle();
    }

    /// <summary>
    /// An image another site asked for is refused.
    /// </summary>
    /// <remarks>
    /// The half that keeps the relaxation honest. A third-party page embedding our address gets
    /// <c>cross-site</c> from its own browser and cannot say otherwise — and its request would carry
    /// no cookie either, the session being <c>SameSite=Strict</c>.
    /// </remarks>
    [Fact]
    public async Task An_image_another_site_asked_for_is_refused()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AnonymousPath);

        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        request.Headers.Add("Sec-Fetch-Dest", "image");

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.ProxiedApi.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A write from this origin without the application header is still refused.
    /// </summary>
    /// <remarks>
    /// The boundary of the relaxation, and the fact that makes it defensible: it covers reads and
    /// nothing else. A write is what the guard was built for, and a same-origin attestation does not
    /// excuse one — the header stays the only thing that admits a write.
    /// </remarks>
    [Fact]
    public async Task A_write_from_this_origin_without_the_application_header_is_still_refused()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ForwardedPath);

        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.ProxiedApi.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// A forwarded call without the application header is refused even with a session.
    /// </summary>
    [Fact]
    public async Task A_forwarded_call_without_the_application_header_is_refused_even_with_a_session()
    {
        await SignInAsync();

        // The forgery case exactly: the browser attaches the cookie to a request the application
        // did not make. This one says nothing at all about where it came from — no header, and no
        // Sec-Fetch-Site to speak for it — so neither way past the guard is open to it.
        var response = await _browser.GetAsync(ForwardedPath);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.ProxiedApi.Requests.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- signing out

    /// <summary>
    /// Logout ends the session rather than forgetting it.
    /// </summary>
    [Fact]
    public async Task Logout_ends_the_session_rather_than_forgetting_it()
    {
        await SignInAsync();

        var logout = await SendAsync(HttpMethod.Post, LogoutPath);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "signing out answers a cleared cookie and nothing else");

        var afterwards = await SendAsync(HttpMethod.Get, ForwardedPath);

        afterwards.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the credential lived on the server side of the boundary, so dropping the cookie " +
            "revokes access — clearing localStorage never did");
    }

    /// <summary>
    /// Logout without the application header is refused.
    /// </summary>
    [Fact]
    public async Task Logout_without_the_application_header_is_refused()
    {
        await SignInAsync();

        // Not a variation on the tests above. This endpoint was reachable without the header,
        // because its signature was RequestDelegate's and route-handler filters do not run for
        // one: a cross-site POST could end the session. Asserting the 403 is what holds the
        // signature in place — nothing else here would notice it changing back.
        var response = await _browser.PostAsync(LogoutPath, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------- helpers

    private static HttpContent Credentials() =>
        JsonContent.Create(new { username = "alice", password = "Password1!" });

    private Task<HttpResponseMessage> SignInAsync() =>
        SendAsync(HttpMethod.Post, LoginPath, Credentials());

    /// <summary>Sends what the front end sends: the marker header, always.</summary>
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };

        request.Headers.Add(BffContract.RequestedWithHeader, BffContract.RequestedWithValue);

        return _browser.SendAsync(request);
    }
}
