using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The catalogue-capacity rule over HTTP, on both hosts: the tenth training is created, the
/// eleventh is refused with the domain's own code.
/// </summary>
/// <remarks>
/// The rule itself — no trainer publishes more than <see cref="Training.MaximumPerTrainer"/>
/// trainings — is the aggregate's, proven in the domain suite against a mocked counter. What only
/// this suite can prove is the whole chain: the repository counting real rows, the factory
/// refusing on the real count, and the refusal leaving as a problem document a caller can branch
/// on. Walking to the limit one POST at a time is the price of that proof, paid once here rather
/// than once per host.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogueCapacityTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    /// <summary>
    /// A full catalogue, refuses the next training and no sooner.
    /// </summary>
    [Fact]
    public async Task AFullCatalogue_RefusesTheNextTrainingAndNoSooner()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Walk to the boundary: every training up to and including the last allowed one is
        // accepted. Asserting each answer is the "and no sooner" half of the claim — a guard
        // that tripped at nine would pass a test that only poked the eleventh.
        for (var published = 1; published <= Training.MaximumPerTrainer; published++)
        {
            var accepted = await client.PostAsJsonAsync(
                "/Training", TrainingRequests.Valid($"Catalogue training {published:D2}"));

            accepted.StatusCode.Should().Be(
                HttpStatusCode.Created,
                $"training {published} of {Training.MaximumPerTrainer} is still within the catalogue's capacity");
        }

        var refused = await client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("One training too many"));

        // The refusal is a business failure like any other (ADR 0016): a problem document whose
        // domain code names the rule, so a caller can tell "your catalogue is full" apart from
        // "that title is taken" without parsing prose.
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainingErrorCodes.CatalogueFull.Value);
    }
}
