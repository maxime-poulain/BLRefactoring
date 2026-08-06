using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The validation pipeline of the CQRS stack, seen from the outside.
/// </summary>
/// <remarks>
/// <para>
/// What this pipeline still refuses is one thing, and ADR 0043 is why: the contract declares shape
/// and presence at model binding, the domain judges what a value means, and what is left here is
/// the refusal neither of them can make — an empty identifier. <c>Guid.Empty</c> is a perfectly
/// well-formed <c>Guid</c>, so the contract has no reason to reject it, and by the time the domain
/// sees it <c>EntityId.Create</c> has already thrown, which is a 500 where the caller deserves a
/// 400.
/// </para>
/// <para>
/// These assertions used to be made with a malformed email, back when the CQRS validator judged the
/// shape of an address and the layered host did not — so the same request was refused by two
/// layers, with two codes, depending on which host answered. That divergence is gone (ADR 0043),
/// and what proves it is now a fact in the shared TestKit, run by both suites. What is left here is
/// the part that was always this file's real subject: the <em>shape</em> of a pipeline rejection.
/// </para>
/// <para>
/// It used to throw a <c>ValidationException</c> that <c>ValidationExceptionHandler</c> answered
/// with a <c>ValidationProblemDetails</c> keyed by field. That put two shapes on one endpoint: a
/// rejection left through the exception under <c>errors</c>, while a bio of six hundred characters
/// passed the validator, reached the domain, and left under <c>domainErrors</c>. Same endpoint, two
/// vocabularies, decided by which rule the caller happened to break. The behaviour now returns a
/// failed <c>Result</c>, so where a request was rejected no longer changes how a client reads why.
/// </para>
/// <para>
/// These assertions are made over HTTP rather than by dispatching a command because the shape under
/// test is produced by the controller and the problem-details plumbing, neither of which runs in a
/// unit test. What the behaviour itself returns is covered in <c>ValidationPipelineBehaviorTests</c>.
/// </para>
/// </remarks>
[Collection("Api")]
public sealed class ValidationPipelineTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>
    /// Empty identifier, on a command, is rejected by the validator, as a domain error document.
    /// </summary>
    /// <remarks>
    /// The ownership policy lets a nonexistent training through on purpose — existence is the
    /// action's concern, and failing there would turn "not found" into an incorrect 403 — so the
    /// request reaches the pipeline, which is exactly where this rejection is supposed to happen.
    /// The version travels with a real training's tag: the validator refuses on the identifier
    /// before anything looks at the version, and giving it a well-formed one keeps that the only
    /// reason the request is refused.
    /// </remarks>
    [Fact]
    public async Task EmptyIdentifier_OnACommand_IsRejectedByTheValidator_AsADomainErrorDocument()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);
        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");

        var response = await client.PutWithIfMatchAsync(
            $"/Training/{Guid.Empty}", TrainingRequests.ValidEdition(), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The media type every other failure of this API answers with. A validator rejection used to
        // be the one that did not, because it left through a different handler.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await BodyAsync(response);

        // Under `domainErrors`, like a business failure — not under `errors`, which is the field map
        // RFC 7807 defines and which this endpoint still uses for the data annotations that run
        // ahead of the pipeline.
        body.TryGetProperty("errors", out _)
            .Should().BeFalse("a rejection from the pipeline is not a ValidationProblemDetails");

        var domainErrors = body.GetProperty("domainErrors").EnumerateArray().ToArray();
        domainErrors.Should().NotBeEmpty();

        domainErrors.Select(error => error.GetProperty("errorCode").GetString()!)
            .ToArray()
            .Should().AllBe("Validation");

        // The validator's message carries the field, which is what the field map used to key on.
        domainErrors
            .Select(error => error.GetProperty("errorMessage").GetString()!)
            .Any(message => message.Contains("Training Id", StringComparison.Ordinal))
            .Should().BeTrue("the caller still has to be told which field was refused");
    }

    /// <summary>
    /// Validation runs before the handler, leaving the aggregate untouched.
    /// </summary>
    /// <remarks>
    /// The payload has to be one the pipeline itself refuses, or this asserts something else
    /// entirely. It used to be a malformed email, and when ADR 0043 gave that judgement back to the
    /// domain this test kept passing — for a new reason: the handler ran, the value object refused,
    /// and nothing was saved because the <em>domain</em> said no. The assertion held and the name
    /// stopped being true, which is the quieter half of that change and the reason the payload
    /// moved with the other one.
    /// </remarks>
    [Fact]
    public async Task ValidationRunsBeforeTheHandler_LeavingTheAggregateUntouched()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);
        var before = await client.GetETagAsync($"/Training/{trainingId}");

        var response = await client.PutWithIfMatchAsync(
            $"/Training/{Guid.Empty}", TrainingRequests.ValidEdition(), before);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The behaviour returns before calling the handler, so nothing was saved and the version the
        // caller holds is still current — their next edit must not need a reload. This is the
        // property the move from throwing to returning had to preserve.
        var after = await client.GetETagAsync($"/Training/{trainingId}");
        after.Should().Be(before);
    }

    /// <summary>
    /// Creates a training and answers its identifier.
    /// </summary>
    /// <remarks>
    /// A creation answers 201 with the identifier in the body and the address in <c>Location</c>
    /// (ADR 0011) — no representation, and therefore no <c>ETag</c>. The version comes from reading
    /// the training back, which is the same two steps <c>ConditionalRequestTest</c> takes.
    /// </remarks>
    private static async Task<Guid> CreateTrainingAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid());
        created.EnsureSuccessStatusCode();

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Empty identifier, on a query, is still answered 400.
    /// </summary>
    [Fact]
    public async Task EmptyIdentifier_OnAQuery_IsStillAnswered400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.Empty}");

        // A query has no failed Result to return, so its validators still throw and
        // ValidationExceptionHandler still answers them. That path is why the handler stays
        // registered — and why this is a 400: without the validator, Guid.Empty reaches
        // EntityId.Create, whose constructor throws, and the caller gets a 500. The layered host,
        // which has no such validator, answers exactly that 500 today.
        //
        // It used to ask `/Trainer/{Guid.Empty}`, which stopped being routed when the read by
        // trainer identifier was withdrawn. `GetTrainingByIdQuery` is the query that still takes a
        // Guid off the route, and it validates it the same way — a 400 here proves the same thing
        // about the same path. Note that the ownership clause never comes into it: validation runs
        // before the handler, so the caller is refused for the identifier, not for whose it is.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
