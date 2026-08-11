using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
    private const string RegisterPath = "bff/register";
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
    /// The catalog page is prerendered for a visitor with no session.
    /// </summary>
    /// <remarks>
    /// The whole point of ADR 0072, observed from outside: the response to a plain GET carries the
    /// page's own words as HTML, before any WebAssembly runs — which is all a crawler ever reads.
    /// It doubles as the proof that the host can resolve everything the page and the layout
    /// inject, which is the half of prerendering that fails at runtime rather than at build time.
    /// </remarks>
    [Fact]
    public async Task The_catalog_page_is_prerendered_for_a_visitor_with_no_session()
    {
        var response = await _browser.GetAsync("/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Trainings on offer",
            "the catalog's first paint is served as HTML, for the crawler ADR 0062 invited (ADR 0072)");
    }

    /// <summary>
    /// The home page is not prerendered.
    /// </summary>
    /// <remarks>
    /// The other half of the closed set: everything outside the catalog keeps prerender off,
    /// because those screens are interactive controls behind a sign-in and a prerendered pass
    /// renders them inert. The landing page's words arrive only once WebAssembly boots, so their
    /// absence from the raw HTML is what "off" looks like from outside.
    /// </remarks>
    [Fact]
    public async Task The_home_page_is_not_prerendered()
    {
        var response = await _browser.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync()).Should().NotContain("Trainers publish trainings",
            "outside the catalog the first paint is the WebAssembly boot, not a prerendered form " +
            "that drops clicks (ADR 0072)");
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

    // ---------------------------------------------------------------- the crawler's doors

    /// <summary>
    /// Robots names the sitemap and shields the signed in spaces.
    /// </summary>
    /// <remarks>
    /// Fetched bare — no marker header, no <c>Sec-Fetch-Site</c> — because that is how a crawler
    /// arrives, and these doors exist for exactly that caller (ADR 0073).
    /// </remarks>
    [Fact]
    public async Task Robots_names_the_sitemap_and_shields_the_signed_in_spaces()
    {
        var response = await _browser.GetAsync("/robots.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a crawler sends none of the application's headers, and this door is outside every guard");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Sitemap: https://localhost/sitemap.xml",
            "the sitemap line is absolute, built from the request's own origin");
        body.Should().Contain("Disallow: /administration/")
            .And.Contain("Disallow: /trainings")
            .And.Contain("Disallow: /profile",
                "the signed-in spaces are empty shells to a caller with no session");
    }

    /// <summary>
    /// The sitemap lists what the catalog offers.
    /// </summary>
    [Fact]
    public async Task The_sitemap_lists_what_the_catalog_offers()
    {
        const string TrainingA = "0f8fad5b-d9cb-469f-a165-70867728950e";
        const string TrainingB = "7c9e6679-7425-40de-944b-e07fc1f90ae7";
        const string Trainer = "9d1f7f2e-0e4a-4a1e-9f5f-2b3c4d5e6f70";

        _factory.LoginApi.Respond = _ => RecordingHandler.Ok(
            $$"""
              {"items":[
                  {"id":"{{TrainingA}}","trainerId":"{{Trainer}}","title":"Alpha"},
                  {"id":"{{TrainingB}}","trainerId":"{{Trainer}}","title":"Beta"}],
               "page":1,"pageSize":100,"totalCount":2,"totalPages":1,"hasNextPage":false}
              """);

        var response = await _browser.GetAsync("/sitemap.xml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("<loc>https://localhost/catalog</loc>",
            "the listing itself is the first address a crawler should know");
        body.Should().Contain($"<loc>https://localhost/catalog/{TrainingA}</loc>")
            .And.Contain($"<loc>https://localhost/catalog/{TrainingB}</loc>");
        body.Split($"<loc>https://localhost/catalog/trainers/{Trainer}</loc>").Should().HaveCount(2,
            "a trainer with several trainings on offer is one address, not one per row");

        var forwarded = _factory.LoginApi.Requests.Should().ContainSingle().Subject;

        forwarded.Uri!.Query.Should().Contain("PageSize=100",
            "the sitemap reads at the contract's own maximum — the fewest round trips the API allows");
        forwarded.Authorization.Should().BeNull(
            "the catalog's read is published and anonymous (ADR 0059), and the sitemap rides it");
    }

    /// <summary>
    /// The sitemap says unavailable when the api is down.
    /// </summary>
    [Fact]
    public async Task The_sitemap_says_unavailable_when_the_api_is_down()
    {
        _factory.LoginApi.Respond = _ => throw new HttpRequestException("The API is down.");

        var response = await _browser.GetAsync("/sitemap.xml");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "a sitemap that answers half the truth teaches a crawler that the missing half is gone");
    }

    /// <summary>
    /// A prerendered training page carries its head.
    /// </summary>
    [Fact]
    public async Task A_prerendered_training_page_carries_its_head()
    {
        var trainingId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _factory.LoginApi.Respond = _ => RecordingHandler.Ok(
            $$"""
              {"id":"{{trainingId}}","title":"Domain Modeling","trainerId":"{{trainerId}}",
               "trainerName":"Alice Martin","trainerPhotoId":"{{photoId}}","topics":["Design"],
               "description":"Aggregates, invariants and the language they answer to.",
               "prerequisites":"None.","acquiredSkills":"Modeling."}
              """);

        var response = await _browser.GetAsync($"/catalog/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("rel=\"canonical\"",
            "the head reaches the prerendered HTML, which is all a crawler ever reads (ADR 0073)");
        html.Should().Contain($"https://localhost/catalog/{trainingId}",
            "the canonical names the bare route, whatever address the page was reached by");
        html.Should().Contain($"https://localhost/portraits/trainings/{trainingId}/{photoId}",
            "og:image points at the crawler's door, not at the /api route whose guard refuses an unfurler");
    }

    /// <summary>
    /// A prerendered trainer page carries its head.
    /// </summary>
    [Fact]
    public async Task A_prerendered_trainer_page_carries_its_head()
    {
        var trainerId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _factory.LoginApi.Respond = _ => RecordingHandler.Ok(
            $$"""
              {"id":"{{trainerId}}","firstname":"Alice","lastname":"Martin",
               "bio":"Teaches domain modeling.","photoId":"{{photoId}}",
               "trainings":[{"id":"{{Guid.NewGuid()}}","trainerId":"{{trainerId}}","title":"Alpha"}]}
              """);

        var response = await _browser.GetAsync($"/catalog/trainers/{trainerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("rel=\"canonical\"")
            .And.Contain($"https://localhost/catalog/trainers/{trainerId}",
                "the person's page describes itself the way the training's does (ADR 0073)");
        html.Should().Contain($"https://localhost/portraits/trainers/{trainerId}/{photoId}",
            "og:image points at the crawler's door here too");
    }

    /// <summary>
    /// A trainer nobody offers is marked noindex.
    /// </summary>
    [Fact]
    public async Task A_trainer_nobody_offers_is_marked_noindex()
    {
        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/problem+json")
        };

        var response = await _browser.GetAsync($"/catalog/trainers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("noindex",
            "a profile that answers nothing leaves as HTTP 200, and the tag keeps it out of an index");
        html.Should().NotContain("rel=\"canonical\"");
    }

    /// <summary>
    /// A training nobody offers is marked noindex.
    /// </summary>
    [Fact]
    public async Task A_training_nobody_offers_is_marked_noindex()
    {
        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/problem+json")
        };

        var response = await _browser.GetAsync($"/catalog/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the router is a client component with no status to set, and this soft answer is " +
            "exactly why the tag exists");

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("noindex",
            "without the tag, every mistyped or withdrawn identifier is an indexable page (ADR 0073)");
        html.Should().NotContain("rel=\"canonical\"",
            "a canonical on the nothing-here panel would invite the indexing it is meant to prevent");
    }

    /// <summary>
    /// A portrait is served to a caller with no headers.
    /// </summary>
    /// <remarks>
    /// The defect this fact was written against: <c>og:image</c> pointing at the API's own portrait
    /// route answers 403 to exactly its audience, because a link unfurler sends neither the marker
    /// header nor <c>Sec-Fetch-Site</c> — the pinned behavior of the <c>/api</c> guard. The
    /// pass-through is a narrow door beside that guard, not a relaxation of it (ADR 0073).
    /// </remarks>
    [Fact]
    public async Task A_portrait_is_served_to_a_caller_with_no_headers()
    {
        byte[] pixels = [0x89, 0x50, 0x4E, 0x47];

        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(pixels)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") }
            }
        };

        var trainingId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var response = await _browser.GetAsync($"/portraits/trainings/{trainingId}/{photoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a link unfurler fetching og:image sends none of the application's headers");
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(pixels);
        response.Headers.CacheControl!.ToString().Should().Contain("immutable",
            "the address carries the photo's identity, so the bytes it answers never change (ADR 0063)");

        var forwarded = _factory.LoginApi.Requests.Should().ContainSingle().Subject;

        forwarded.Uri!.AbsolutePath.Should().Be($"/Catalog/trainings/{trainingId}/photo/{photoId}");
        forwarded.Authorization.Should().BeNull(
            "the portrait is published (ADR 0063); there is no credential to attach and none to require");
    }

    /// <summary>
    /// A trainers portrait is served through its own door.
    /// </summary>
    [Fact]
    public async Task A_trainers_portrait_is_served_through_its_own_door()
    {
        var trainerId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var response = await _browser.GetAsync($"/portraits/trainers/{trainerId}/{photoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.LoginApi.Requests.Should().ContainSingle()
            .Which.Uri!.AbsolutePath.Should().Be($"/Catalog/trainers/{trainerId}/photo/{photoId}",
                "the trainer's door forwards to the trainer's published route, as the training's does");
    }

    /// <summary>
    /// A portrait the api refuses answers the apis verdict.
    /// </summary>
    [Fact]
    public async Task A_portrait_the_api_refuses_answers_the_apis_verdict()
    {
        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var response = await _browser.GetAsync($"/portraits/trainings/{Guid.NewGuid()}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a photo that was replaced answers 404 here for the same reason it does at the API");
        response.Headers.CacheControl.Should().BeNull(
            "a year of immutable is a promise about bytes, and a refusal carries none");
    }

    /// <summary>
    /// A portrait answers unavailable when the api is down.
    /// </summary>
    [Fact]
    public async Task A_portrait_answers_unavailable_when_the_api_is_down()
    {
        _factory.LoginApi.Respond = _ => throw new HttpRequestException("The API is down.");

        var response = await _browser.GetAsync($"/portraits/trainings/{Guid.NewGuid()}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
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
        // ExpiresUtc, taken from the JWT's own `exp` — and nothing in a Set-Cookie header can show
        // it. So the ticket is opened rather than believed: the assertion below unprotects the
        // cookie with the host's own ticket format, because deleting the one line that copies the
        // expiry changes no header and, until this test, failed nothing — the handler would fall
        // back to its fourteen-day default and the session would outlive the token by two weeks.
        cookie.Should().NotContainEquivalentOf("expires=");
        cookie.Should().NotContainEquivalentOf("max-age=");

        var ticket = Unprotect(cookie);
        var expiry = new JwtSecurityTokenHandler().ReadJwtToken(_token).ValidTo;

        ticket.Properties.ExpiresUtc.Should().Be(new DateTimeOffset(expiry),
            "the cookie dies with the token it carries (ADR 0009), and this line is the half of " +
            "that sentence the Set-Cookie header cannot prove");
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

    // ---------------------------------------------------------------- registering

    /// <summary>
    /// Registration is open to a visitor who has never signed in.
    /// </summary>
    /// <remarks>
    /// The defect this fact was written against: the registration page used to call
    /// <c>/api/Auth/register</c>, which the proxy's catch-all route answers with a 401 for anyone
    /// without a session — and creating an account is the one thing a visitor with a session never
    /// does. The BFF now owns the call, beside sign-in, so the proxy's anonymous family stays
    /// exactly the catalog's (ADR 0062).
    /// </remarks>
    [Fact]
    public async Task Registration_is_open_to_a_visitor_who_has_never_signed_in()
    {
        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.Created);

        var response = await SendAsync(HttpMethod.Post, RegisterPath, Registration());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var forwarded = _factory.LoginApi.Requests.Should().ContainSingle().Subject;

        forwarded.Uri!.AbsolutePath.Should().Be("/Auth/register");
        forwarded.Authorization.Should().BeNull("a visitor creating an account has no credential to send");
    }

    /// <summary>
    /// Registration passes the apis problem document through.
    /// </summary>
    /// <remarks>
    /// The opposite of sign-in's rule. A login failure's document describes a call the browser
    /// never made, so only the status is passed; a registration failure's document describes the
    /// very fields the browser submitted — a taken email, a refused password — and the form reads
    /// the per-field messages out of it.
    /// </remarks>
    [Fact]
    public async Task Registration_passes_the_apis_problem_document_through()
    {
        const string Problem =
            """{"title":"One or more validation errors occurred.","status":409,"errors":{"Email":["Email 'alice@example.com' is already taken."]}}""";

        _factory.LoginApi.Respond = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(Problem, System.Text.Encoding.UTF8, "application/problem+json")
        };

        var response = await SendAsync(HttpMethod.Post, RegisterPath, Registration());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("already taken",
            "the per-field messages are what the form renders, and they exist nowhere but in this document");
    }

    /// <summary>
    /// Registration without the application header never reaches the api.
    /// </summary>
    [Fact]
    public async Task Registration_without_the_application_header_never_reaches_the_api()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath)
        {
            Content = Registration()
        };

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.LoginApi.Requests.Should().BeEmpty();
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
    /// A head request this page asked for is admitted as a safe read.
    /// </summary>
    /// <remarks>
    /// The relaxation admits both safe methods, not just GET — a preload or a cache revalidation
    /// asks with HEAD and carries no application header, exactly as an image does. Asserted
    /// because the guard names the two methods one by one, and dropping HEAD would refuse those
    /// requests while every other fact here stayed green.
    /// </remarks>
    [Fact]
    public async Task A_head_request_this_page_asked_for_is_admitted_as_a_safe_read()
    {
        var request = new HttpRequestMessage(HttpMethod.Head, AnonymousPath);

        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.ProxiedApi.Requests.Should().ContainSingle();
    }

    /// <summary>
    /// A read a sibling site asked for is refused.
    /// </summary>
    /// <remarks>
    /// <c>same-site</c> is not <c>same-origin</c>: a page on a sibling subdomain earns the former
    /// and must be refused all the same, because the attestation the guard trusts is "this very
    /// origin asked" and nothing looser. The suite sent <c>same-origin</c> and <c>cross-site</c>;
    /// the value between them is the one a typo in the guard would quietly admit.
    /// </remarks>
    [Fact]
    public async Task A_read_a_sibling_site_asked_for_is_refused()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AnonymousPath);

        request.Headers.Add("Sec-Fetch-Site", "same-site");
        request.Headers.Add("Sec-Fetch-Dest", "image");

        var response = await _browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.ProxiedApi.Requests.Should().BeEmpty();
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
    /// Logout clears the cookie rather than leaving it to expire.
    /// </summary>
    /// <remarks>
    /// The sibling fact asserts the session is over — a later call answers 401. This one asserts
    /// the browser is told so: sign-out's 204 must carry the <c>Set-Cookie</c> that deletes
    /// <c>__Host-bff</c>, or the browser keeps sending a dead ticket until it expires on its own.
    /// </remarks>
    [Fact]
    public async Task Logout_clears_the_cookie_rather_than_leaving_it_to_expire()
    {
        await SignInAsync();

        var logout = await SendAsync(HttpMethod.Post, LogoutPath);

        var cookie = logout.Headers.GetValues("Set-Cookie").Single();

        cookie.Should().StartWith("__Host-bff=;", "signing out empties the cookie");
        cookie.Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970",
            "a deletion is an expiry in the past — anything else leaves the cookie in the jar");
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

    // ---------------------------------------------------------------- the host's own wiring

    /// <summary>
    /// The bff refuses a configuration without the apis address.
    /// </summary>
    /// <remarks>
    /// The guard in <c>AddBff</c> exists so a missing address fails at startup, loudly, instead of
    /// as an obscure connection error on the first sign-in. Asserted against the extension
    /// directly rather than a rebuilt host: the factory's configuration hooks run before the
    /// application's own files load, so a host-level override cannot make the value disappear —
    /// and the guard is a line of <c>AddBff</c>, not of the host around it.
    /// </remarks>
    [Fact]
    public void The_bff_refuses_a_configuration_without_the_apis_address()
    {
        var registering = () => new ServiceCollection().AddBff(new ConfigurationBuilder().Build());

        registering.Should().Throw<InvalidOperationException>()
            .WithMessage("*Api:BaseAddress*",
                "the BFF forwards to the API and has no sensible default — a localhost address in " +
                "production would fail obscurely, and silently");
    }

    /// <summary>
    /// An authorization denial answers 403 rather than a redirect.
    /// </summary>
    /// <remarks>
    /// The caller is a fetch client, so the cookie handler's two redirects are rewritten as status
    /// codes. The 401 half is exercised by every fact that calls without a session; the 403 half —
    /// <c>OnRedirectToAccessDenied</c> — had no path through the production routes, so this fact
    /// drives the handler's forbid directly and asserts the rewrite, not a 302 to a page that does
    /// not exist server-side.
    /// </remarks>
    [Fact]
    public async Task An_authorization_denial_answers_403_rather_than_a_redirect()
    {
        using var probed = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new ForbidProbe())));
        using var browser = probed.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await browser.GetAsync(ForbidProbe.Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a denial is a status code here, never a redirect to an AccessDenied page");
        response.Headers.Location.Should().BeNull();
    }

    /// <summary>
    /// Prepends one probe endpoint that asks the cookie handler to forbid, so the rewrite of the
    /// AccessDenied redirect is observable without inventing a production route that denies.
    /// </summary>
    private sealed class ForbidProbe : IStartupFilter
    {
        public const string Path = "/forbid-probe";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == Path)
                    {
                        await context.ForbidAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    await nextMiddleware(context);
                });
                next(app);
            };
    }

    // ---------------------------------------------------------------- helpers

    private static HttpContent Credentials() =>
        JsonContent.Create(new { username = "alice", password = "Password1!" });

    private static HttpContent Registration() =>
        JsonContent.Create(new
        {
            username = "alice",
            email = "alice@example.com",
            password = "Password1!",
            confirmPassword = "Password1!",
            firstname = "Alice",
            lastname = "Martin"
        });

    /// <summary>The ticket inside the session cookie, opened with the host's own format.</summary>
    private AuthenticationTicket Unprotect(string setCookie)
    {
        const string Name = "__Host-bff=";
        var value = setCookie[Name.Length..setCookie.IndexOf(';', StringComparison.Ordinal)];

        return _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme)
            .TicketDataFormat
            .Unprotect(value)!;
    }

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
