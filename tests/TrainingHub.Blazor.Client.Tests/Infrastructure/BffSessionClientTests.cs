using System.Net;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// Behavior covered for signing in and out through the BFF.
/// </summary>
/// <remarks>
/// The generated client is not used for this and cannot be: the API answers <c>/Auth/login</c> with
/// the token, and a generated method returning it would put the credential back in the browser —
/// the thing ADR 0009 exists to prevent. What is left to prove is the line this class draws between
/// two kinds of failure: refused credentials are an ordinary answer the sign-in page renders, and
/// everything else is a fault the page must not dress up as "wrong password".
/// </remarks>
public sealed class BffSessionClientTests
{
    /// <summary>
    /// Login, accepted, answers true.
    /// </summary>
    [Fact]
    public async Task Login_Accepted_AnswersTrue()
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(HttpStatusCode.NoContent)));

        // Act
        var signedIn = await client.LoginAsync("john", "secret");

        // Assert
        signedIn.Should().BeTrue();
    }

    /// <summary>
    /// Login, credentials refused, answers false rather than throwing.
    /// </summary>
    /// <remarks>
    /// Both statuses, because the two are one outcome to a person: 401 is the API refusing the
    /// pair, 400 is Identity refusing the shape of it. A page that treated 400 as a fault would
    /// tell somebody the service is down when they mistyped their username.
    /// </remarks>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Login_CredentialsRefused_AnswersFalseRatherThanThrowing(HttpStatusCode status)
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(status)));

        // Act
        var signedIn = await client.LoginAsync("john", "wrong");

        // Assert
        signedIn.Should().BeFalse();
    }

    /// <summary>
    /// Login, the BFF failed, throws rather than reading as a refusal.
    /// </summary>
    /// <remarks>
    /// The distinction that matters: answering <see langword="false"/> here would render "Invalid
    /// username or password" for an outage, and the user would spend the evening retyping a
    /// password that was right the first time.
    /// </remarks>
    [Fact]
    public async Task Login_TheBffFailed_ThrowsRatherThanReadingAsARefusal()
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(HttpStatusCode.BadGateway)));

        // Act
        var act = async () => await client.LoginAsync("john", "secret");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Login, posts to the BFF's own endpoint and not to the API.
    /// </summary>
    /// <remarks>
    /// The address is the security property. <c>/api/Auth/login</c> would be forwarded to the API
    /// and answer with the token in the body, which is exactly what this class exists to avoid.
    /// </remarks>
    [Fact]
    public async Task Login_PostsToTheBffsOwnEndpoint()
    {
        // Arrange
        var factory = new StubHttpClientFactory(_ => Status(HttpStatusCode.NoContent));

        // Act
        await new BffSessionClient(factory).LoginAsync("john", "secret");

        // Assert
        factory.ClientNames.Should().Equal(HttpClientNames.Bff);
        factory.Requests.Should().ContainSingle().Which.Should().Match<HttpRequestMessage>(
            request => request.Method == HttpMethod.Post
                       && request.RequestUri!.AbsolutePath == "/bff/login");
    }

    /// <summary>
    /// Register, accepted, answers no problem and posts to the BFF's own endpoint.
    /// </summary>
    /// <remarks>
    /// The address is the defect this method was written against: the generated client's
    /// <c>/api/Auth/register</c> is behind the proxy's session-guarded route, and a visitor
    /// creating an account never has a session.
    /// </remarks>
    [Fact]
    public async Task Register_Accepted_AnswersNoProblemAndPostsToTheBffsOwnEndpoint()
    {
        // Arrange
        var factory = new StubHttpClientFactory(_ => Status(HttpStatusCode.Created));

        // Act
        var problem = await new BffSessionClient(factory).RegisterAsync(Registration());

        // Assert
        problem.Should().BeNull();
        factory.Requests.Should().ContainSingle().Which.Should().Match<HttpRequestMessage>(
            request => request.Method == HttpMethod.Post
                       && request.RequestUri!.AbsolutePath == "/bff/register");
    }

    /// <summary>
    /// Register, refused, answers the problem document rather than throwing.
    /// </summary>
    /// <remarks>
    /// A refusal is an ordinary answer here — a taken email, a password the rules decline — and
    /// the per-field messages inside the document are what the page renders. Throwing would turn
    /// every validation verdict into an outage banner.
    /// </remarks>
    [Fact]
    public async Task Register_Refused_AnswersTheProblemDocumentRatherThanThrowing()
    {
        // Arrange
        const string Problem =
            """{"title":"One or more validation errors occurred.","status":409,"errors":{"Email":["Email 'john@example.com' is already taken."]}}""";

        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(Problem, System.Text.Encoding.UTF8, "application/problem+json")
        });

        // Act
        var document = await new BffSessionClient(factory).RegisterAsync(Registration());

        // Assert
        document.Should().NotBeNull();
        document.Title.Should().Be("One or more validation errors occurred.");
        document.AdditionalProperties.Should().ContainKey("errors",
            "the per-field map is the part of the document the form reads");
    }

    /// <summary>
    /// Register, the BFF failed without a document, throws rather than reading as a refusal.
    /// </summary>
    [Fact]
    public async Task Register_TheBffFailedWithoutADocument_ThrowsRatherThanReadingAsARefusal()
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(HttpStatusCode.BadGateway)));

        // Act
        var act = async () => await client.RegisterAsync(Registration());

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Logout, the session had already expired, succeeds.
    /// </summary>
    /// <remarks>
    /// Signing out of nothing did what the caller asked: they wanted to end up signed out, and they
    /// are. Throwing would put an error on screen at the exact moment the user is leaving.
    /// </remarks>
    [Fact]
    public async Task Logout_TheSessionHadAlreadyExpired_Succeeds()
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(HttpStatusCode.Unauthorized)));

        // Act
        var act = async () => await client.LogoutAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Logout, the BFF failed, throws.
    /// </summary>
    /// <remarks>
    /// The counterpart to the case above, and the reason it is written as one status rather than
    /// "any failure is fine": a 500 means the cookie may still be live, and reporting a clean
    /// sign-out then would be a lie about a security boundary.
    /// </remarks>
    [Fact]
    public async Task Logout_TheBffFailed_Throws()
    {
        // Arrange
        var client = new BffSessionClient(new StubHttpClientFactory(_ => Status(HttpStatusCode.InternalServerError)));

        // Act
        var act = async () => await client.LogoutAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static TrainingHub.GeneratedClients.RegisterHttpRequest Registration() => new()
    {
        Username = "john",
        Email = "john@example.com",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        Firstname = "John",
        Lastname = "Doe"
    };
}
