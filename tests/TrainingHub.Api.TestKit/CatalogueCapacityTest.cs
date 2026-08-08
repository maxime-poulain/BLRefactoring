using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
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
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
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
    /// The withholding happens through a scope rather than over HTTP because no endpoint reaches it
    /// yet: the administrative use cases arrive with the commit that gives them controllers. What
    /// is under test here is the quota, and the quota does not care which caller moved the state —
    /// the same reason <c>DomainEventPipelineTest</c> drives the cascade through the container.
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
                "/Training", TrainingRequests.Valid($"Catalogue training {published:D2}"));

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
            .Should().Contain(TrainingErrorCodes.CatalogueFull.Value);
    }

    /// <remarks>
    /// Its own scope, as a separate request would be, and committed through the host's own unit of
    /// work — so the count the next POST triggers reads a row rather than a change tracker.
    /// </remarks>
    private async Task WithholdAsync(Guid trainingId)
    {
        using var scope = Factory.CreateScope();

        var trainings = scope.ServiceProvider.GetRequiredService<ITrainingRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var training = await trainings.GetByIdAsync(TrainingId.Create(trainingId))
            ?? throw new InvalidOperationException($"The fixture found no training {trainingId}.");

        var reason = WithholdingReason.Create("Withheld for the purposes of this proof.")
            .Match(value => value, _ => throw new InvalidOperationException(
                "The fixture could not build a withholding reason."));

        training.Withhold(reason).Switch(
            () => { },
            errors => throw new InvalidOperationException(
                "The fixture could not withhold a training: "
                + string.Join(", ", errors.Select(error => error.ErrorMessage))));

        await unitOfWork.SaveChangesAsync();
    }
}
