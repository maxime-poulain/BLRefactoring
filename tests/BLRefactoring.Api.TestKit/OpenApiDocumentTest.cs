using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// The OpenAPI document each host publishes.
/// </summary>
/// <remarks>
/// Run against both suites, because the point of the decision it guards is that the two hosts
/// describe the same API the same way. They did not: one generated its document with NSwag and the
/// other with Swashbuckle, and the second declared no security scheme at all — so its reference UI
/// could not offer a token and no authenticated endpoint was reachable from it. Nothing failed;
/// the document was simply wrong, quietly, on one host only. See ADR 0006.
/// <para>
/// Asserted over HTTP rather than by inspecting the generator's services, because what matters is
/// the document a client downloads. The endpoints are mapped in Development only, which is the
/// environment <c>ApiFactory</c> runs its host in.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the document each one
/// produces is precisely what is under test.</typeparam>
public abstract class OpenApiDocumentTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    private const string DocumentPath = "/openapi/v1.json";

    private async Task<JsonElement> DocumentAsync()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(DocumentPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the document is what generated clients are built from");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Document_IsServed_AndDescribesTheSharedRoutes()
    {
        var document = await DocumentAsync();

        document.GetProperty("openapi").GetString().Should().StartWith("3.");

        var paths = document.GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .ToArray();

        // Declared on AuthControllerBase, which both hosts derive from, so a document missing them
        // has not seen the controllers at all rather than merely differing from its sibling.
        paths.Should().Contain("/Auth/login");
        paths.Should().Contain("/Auth/register");
        paths.Should().Contain("/Training");
    }

    [Fact]
    public async Task Document_DeclaresHowTheApiIsAuthenticated()
    {
        var document = await DocumentAsync();

        // The defect this guards: a bare AddSwaggerGen() produces a perfectly valid document that
        // never mentions bearer authentication, so a reader has no way to supply a token.
        var scheme = document
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        scheme.GetProperty("type").GetString().Should().Be("http");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");
        scheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }

    /// <summary>
    /// The request half of the conditional-request contract.
    /// </summary>
    /// <remarks>
    /// This is the assertion that would have caught the defect ADR 0010 repairs. The header was
    /// read straight off <c>Request.Headers</c>, so it never entered the document, so the generated
    /// client had no parameter for it and could not send one — and every edit from the front end
    /// came back 428. Nothing failed anywhere: the API worked, the client compiled, the document
    /// was valid. It simply did not describe the one thing a caller had to do.
    /// </remarks>
    [Fact]
    public async Task Document_DeclaresTheIfMatchHeader_OnConditionalWrites()
    {
        var document = await DocumentAsync();

        var parameters = Property(
                Property(Property(document, "paths"), "/Training/{trainingId}"),
                "put")
            .GetProperty("parameters")
            .EnumerateArray()
            .ToArray();

        parameters.Should().Contain(
            parameter => parameter.GetProperty("in").GetString() == "header"
                         && parameter.GetProperty("name").GetString() == "If-Match",
            "a generated client can only send what the document declares");
    }

    /// <summary>
    /// The response half. Knowing a version must be sent is no use without knowing where it comes
    /// from.
    /// </summary>
    [Fact]
    public async Task Document_DeclaresTheETagHeader_OnTheReadsThatPublishOne()
    {
        var document = await DocumentAsync();

        var success = Property(
            Property(
                Property(Property(Property(document, "paths"), "/Training/{trainingId}"), "get"),
                "responses"),
            "200");

        var etag = Property(Property(success, "headers"), "ETag");

        etag.GetProperty("schema").GetProperty("type").GetString().Should().Be("string");
    }

    /// <summary>
    /// Registration is a creation, and the document says so.
    /// </summary>
    /// <remarks>
    /// It used to say 204. A generated client built from that document had no way to learn the
    /// identifier of what it had just created, and no reason to look at <c>Location</c> — the
    /// status told it there was nothing to read.
    /// </remarks>
    [Fact]
    public async Task Document_DescribesRegistrationAsACreation()
    {
        var document = await DocumentAsync();

        var responses = Property(
            Property(Property(Property(document, "paths"), "/Auth/register"), "post"),
            "responses");

        responses.TryGetProperty("201", out _).Should().BeTrue(
            "registration creates a trainer and publishes its address");
        responses.TryGetProperty("409", out _).Should().BeTrue(
            "a username or email already taken is a conflict, not a malformed request");
        responses.TryGetProperty("204", out _).Should().BeFalse(
            "the creation no longer answers as though it had produced nothing");
    }

    /// <summary>
    /// The response every authenticated operation is guaranteed to be able to produce.
    /// </summary>
    /// <remarks>
    /// Declared once on <c>ApiControllerBase</c> rather than on each action; this asserts that
    /// MVC carries a class-level declaration from a base controller into the document, which is
    /// the whole reason it is written in one place.
    /// </remarks>
    [Fact]
    public async Task Document_DeclaresTheUnauthorizedResponse_OnAuthenticatedOperations()
    {
        var document = await DocumentAsync();

        var responses = Property(
            Property(Property(Property(document, "paths"), "/Training/{trainingId}"), "get"),
            "responses");

        responses.TryGetProperty("401", out var unauthorized).Should().BeTrue(
            "a client that cannot see the answer it will most often meet handles it as a surprise");

        // The half that is easy to get wrong. [ApiController] types every unstated error response
        // as ProblemDetails, and the 401 is written by the authentication middleware with no body:
        // a client told to expect one deserialises nothing and throws about the nothing, instead of
        // reporting the 401 it was handed.
        unauthorized.TryGetProperty("content", out _).Should().BeFalse(
            "the 401 the middleware writes carries no body");
    }

    /// <summary>
    /// The same, for the status the ownership policy produces.
    /// </summary>
    /// <remarks>
    /// This one was already wrong before the 401 existed: two operations declared a 403 and the
    /// document gave it a problem document it never carries.
    /// </remarks>
    [Fact]
    public async Task Document_DeclaresTheForbiddenResponse_WithoutABody()
    {
        var document = await DocumentAsync();

        var responses = Property(
            Property(Property(Property(document, "paths"), "/Training/{trainingId}"), "put"),
            "responses");

        responses.TryGetProperty("403", out var forbidden).Should().BeTrue(
            "the ownership policy answers it and a caller has to tell it apart from a 404");

        forbidden.TryGetProperty("content", out _).Should().BeFalse(
            "the 403 the authorization middleware writes carries no body");
    }

    /// <summary>
    /// The two hosts publish the same operation identifiers, not merely the same routes.
    /// </summary>
    /// <remarks>
    /// <c>operationId</c> is <c>Controller_Action</c>, so a C# method name is a published contract
    /// (ADR 0008 says so explicitly). The CQRS host called two of its actions <c>EditTrainingAsync</c>
    /// and <c>DeleteAsync</c> where the layered host called them <c>UpdateTrainingAsync</c> and
    /// <c>DeleteTrainingAsync</c>, so the two documents disagreed on the name of two operations and
    /// ADR 0006's headline consequence — one generated client fits both — was false.
    /// <para>
    /// Asserted as a fixed list rather than by comparing the two documents, because a test can only
    /// see the host it runs against. Running the same list on both is what makes them agree.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("/Training", "post", "Training_CreateTraining")]
    [InlineData("/Training/{trainingId}", "get", "Training_GetTrainingById")]
    [InlineData("/Training/{trainingId}", "put", "Training_UpdateTraining")]
    [InlineData("/Training/{trainingId}", "delete", "Training_DeleteTraining")]
    [InlineData("/Trainer/me", "get", "Trainer_GetCurrent")]
    [InlineData("/Trainer/me", "put", "Trainer_EditCurrent")]
    [InlineData("/Trainer/{trainerId}", "get", "Trainer_GetById")]
    public async Task Document_PublishesTheSameOperationId_OnBothHosts(
        string path, string verb, string operationId)
    {
        var document = await DocumentAsync();

        var operation = Property(Property(Property(document, "paths"), path), verb);

        operation.GetProperty("operationId").GetString().Should().Be(
            operationId,
            "a renamed action renames the published operation, and the two hosts must not disagree");
    }

    /// <summary>
    /// Reads a member, failing with the path asked for rather than a bare
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    private static JsonElement Property(JsonElement element, string name)
    {
        element.TryGetProperty(name, out var value)
            .Should().BeTrue($"the document should declare '{name}'");

        return value;
    }
}
