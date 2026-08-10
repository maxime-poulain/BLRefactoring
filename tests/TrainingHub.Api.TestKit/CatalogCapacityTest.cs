using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The catalog-capacity rule over HTTP, on both hosts: the tenth training is created, the
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
public abstract class CatalogCapacityTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    /// <summary>
    /// A full catalog, refuses the next training and no sooner.
    /// </summary>
    [Fact]
    public async Task AFullCatalog_RefusesTheNextTrainingAndNoSooner()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Walk to the boundary: every training up to and including the last allowed one is
        // accepted. Asserting each answer is the "and no sooner" half of the claim — a guard
        // that tripped at nine would pass a test that only poked the eleventh.
        for (var published = 1; published <= Training.MaximumPerTrainer; published++)
        {
            var accepted = await client.PostAsJsonAsync(
                "/Training", TrainingRequests.Valid($"Catalog training {published:D2}"));

            accepted.StatusCode.Should().Be(
                HttpStatusCode.Created,
                $"training {published} of {Training.MaximumPerTrainer} is still within the catalog's capacity");
        }

        var refused = await client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("One training too many"));

        // The refusal is a business failure like any other (ADR 0016): a problem document whose
        // domain code names the rule, so a caller can tell "your catalog is full" apart from
        // "that title is taken" without parsing prose.
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainingErrorCodes.CatalogFull.Value);
    }

    /// <summary>
    /// A withheld training, still holds its place in the quota.
    /// </summary>
    /// <remarks>
    /// The half of ADR 0052 that had to land in the same commit as the state itself, proven where
    /// only this suite can prove it: the specification, the repository's real count and the
    /// factory's refusal, over the wire. Withdrawing frees a slot and being moderated does not —
    /// otherwise being moderated would hand a trainer at the limit room for a replacement, which is
    /// a perverse incentive rather than a lifecycle.
    /// <para>
    /// The withholding goes through the administrative endpoint, by an administrator's own token.
    /// It went through a scope while no endpoint reached it, which proved the quota and nothing
    /// about the caller; now that the use cases exist, the whole chain is one run — two callers,
    /// two tokens, and a rule neither of them can see.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AWithheldTraining_StillHoldsItsPlaceInTheQuota()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var first = Guid.Empty;

        for (var published = 1; published <= Training.MaximumPerTrainer; published++)
        {
            var created = await client.PostAsJsonAsync(
                "/Training", TrainingRequests.Valid($"Catalog training {published:D2}"));

            created.EnsureSuccessStatusCode();

            if (published == 1)
            {
                first = await created.Content.ReadFromJsonAsync<Guid>();
            }
        }

        await WithholdAsync(first);

        var refused = await client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("The replacement moderation must not permit"));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a slot freed by being moderated would reward the moderation");

        var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainingErrorCodes.CatalogFull.Value);
    }

    /// <remarks>
    /// A separate request by a separate caller, which is what makes the count the next POST triggers
    /// read a committed row rather than a change tracker shared with it.
    /// </remarks>
    private async Task WithholdAsync(Guid trainingId)
    {
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var withheld = await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = "Withheld for the purposes of this proof." });

        withheld.EnsureSuccessStatusCode();
    }
}
