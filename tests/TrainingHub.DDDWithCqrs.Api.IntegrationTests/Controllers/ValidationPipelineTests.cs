using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using TrainingHub.Shared.Api.Contracts.Trainers;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The validation pipeline of the CQRS stack, seen from the outside.
/// </summary>
/// <remarks>
/// <para>
/// A bad field on the layered host is rejected by the value objects and surfaces as the domain's
/// own error collection. Here it is rejected earlier, by a FluentValidation validator running
/// inside <c>ValidationPipelineBehavior</c> — but it now surfaces the same way, because the
/// behaviour returns a failed <c>Result</c> instead of throwing. Where a request was rejected no
/// longer changes how a client reads why.
/// </para>
/// <para>
/// It used to throw a <c>ValidationException</c> that <c>ValidationExceptionHandler</c> answered
/// with a <c>ValidationProblemDetails</c> keyed by field. That put two shapes on one endpoint: a
/// malformed email left through the exception under <c>errors</c>, while a bio of six hundred
/// characters passed the validator, reached the domain, and left under <c>domainErrors</c>. Same
/// endpoint, two vocabularies, decided by which rule the caller happened to break.
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
    /// Invalid email, is rejected by the validator, as a domain error document.
    /// </summary>
    [Fact]
    public async Task InvalidEmail_IsRejectedByTheValidator_AsADomainErrorDocument()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "not-an-email"
        }, entityTag);

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
            .Any(message => message.Contains("Contact Email", StringComparison.Ordinal))
            .Should().BeTrue("the caller still has to be told which field was refused");
    }

    /// <summary>
    /// Validation runs before the handler, leaving the aggregate untouched.
    /// </summary>
    [Fact]
    public async Task ValidationRunsBeforeTheHandler_LeavingTheAggregateUntouched()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var before = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "Rejected",
            Lastname = "Edit",
            ContactEmail = "not-an-email"
        }, before);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The behaviour returns before calling the handler, so nothing was saved and the version the
        // caller holds is still current — their next edit must not need a reload. This is the
        // property the move from throwing to returning had to preserve.
        var after = await client.GetETagAsync("/Trainer/me");
        after.Should().Be(before);
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
