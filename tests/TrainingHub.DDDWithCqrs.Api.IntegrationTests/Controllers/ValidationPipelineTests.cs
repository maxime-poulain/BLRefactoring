using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// What an empty identifier does to a write, seen from the outside.
/// </summary>
/// <remarks>
/// <para>
/// This file used to assert the <em>shape</em> of a pipeline rejection: that a command refused by
/// FluentValidation left as a <c>domainErrors</c> document like any business failure, rather than
/// through a second error vocabulary keyed by field (ADR 0016). It made that assertion with
/// <c>PUT /Training/{Guid.Empty}</c>, because an empty identifier was the one thing the pipeline
/// still refused and the boundary did not.
/// </para>
/// <para>
/// ADR 0046 closed that gap, and closed this window with it. The route identifier now carries
/// <c>[NotEmptyIdentifier]</c>, so <c>Guid.Empty</c> is refused at model binding and the pipeline
/// never sees it. Every other rule the validators hold guards an identifier that is equally
/// unreachable from here: from a route or a body the contract already refuses, from the token, or
/// minted by the command itself. <strong>No pipeline rejection is reachable over HTTP any more</strong>,
/// which is not a gap in the tests but the point of the record — those rules exist for callers that
/// never cross this boundary, and a caller that never crosses it cannot be simulated by crossing it.
/// What they refuse is asserted directly, per validator, in <c>TrainingHub.DDDWithCqrs.Tests</c>.
/// </para>
/// <para>
/// The assertion that survives is the one that was never about which layer answered: a refused write
/// leaves the aggregate exactly as it was. It is made over HTTP rather than by dispatching a
/// command because that is the only way to observe the version a caller holds afterwards.
/// </para>
/// <para>
/// This is the second time this file's subject has moved out from under it. ADR 0043 gave the
/// judgment of an address back to the domain, and the test kept passing for a new reason — the
/// handler ran and the value object refused. The remark below recorded that trap; this rewrite is
/// what honoring it looks like.
/// </para>
/// </remarks>
[Collection("Api")]
public sealed class ValidationPipelineTests(ApiFactory factory) : IntegrationTest(factory)
{
    /// <summary>
    /// A refused identifier, leaves the aggregate untouched.
    /// </summary>
    /// <remarks>
    /// The refusal now happens at model binding rather than in the pipeline, and the property under
    /// test is indifferent to which: nothing was saved, and the version the caller already holds is
    /// still current, so their next edit does not need a reload. That is what the move from throwing
    /// to returning a failed <c>Result</c> had to preserve, and it is what moving the guard forward
    /// to the contract must not break either.
    /// </remarks>
    [Fact]
    public async Task ARefusedIdentifier_LeavesTheAggregateUntouched()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);
        var before = await client.GetETagAsync($"/Training/{trainingId}");

        var response = await client.PutWithIfMatchAsync(
            $"/Training/{Guid.Empty}", TrainingRequests.ValidEdition(), before);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

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
}
