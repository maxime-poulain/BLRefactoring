using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// A training's life over HTTP: created, read, edited under a precondition, withdrawn and offered
/// again, deleted — and refused to anyone who does not own it.
/// </summary>
/// <remarks>
/// Thirteen facts that used to be written twice, once per suite. They had already begun to drift
/// apart in the way two copies always do: neither copy was wrong, each simply asserted a little
/// more than the other. The CQRS copy checked the <c>ETag</c> on a read and the updated
/// representation on an edit; the layered copy checked that a refused delete leaves the row where
/// it was. Both halves are here, so both hosts now answer for the stronger claim.
/// <para>
/// That is the argument for the kit in one paragraph: a copy per suite is not two proofs of one
/// fact, it is two facts that happen to agree today.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class TrainingLifecycleTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    // One definition of "a valid training", from the kit, rather than the copies of the same object
    // literal the two suites used to carry between them.
    private static CreateTrainingHttpRequest ValidCreation(string? title = null) =>
        TrainingRequests.Valid(title ?? $"Training {Guid.NewGuid():N}"[..25]);

    private static EditTrainingHttpRequest ValidEdition(string? title = null) => new()
    {
        Title = title ?? $"Updated {Guid.NewGuid():N}"[..25],
        Description = "Updated description for the training",
        Prerequisites = "Updated prerequisites",
        AcquiredSkills = "Updated acquired skills",
        Topics = ["Design"]
    };

    private static async Task<Guid> CreateTrainingAsync(HttpClient client, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/Training", ValidCreation(title));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    // -- Create --

    /// <summary>
    /// Create, valid data, returns 201.
    /// </summary>
    /// <remarks>
    /// ADR 0011 cites this endpoint as the precedent for answering a creation with the address of
    /// what was created. On the CQRS host the identifier is the one the command generated before it
    /// was dispatched, so the <c>Location</c> has to name that one and not another.
    /// </remarks>
    [Fact]
    public async Task Create_ValidData_Returns201()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var trainingId = await response.Content.ReadFromJsonAsync<Guid>();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Be($"/Training/{trainingId}");
    }

    /// <summary>
    /// Create, invalid data, returns 400.
    /// </summary>
    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", new CreateTrainingHttpRequest
        {
            Title = "ab",
            Description = "",
            Prerequisites = "",
            AcquiredSkills = "",
            Topics = []
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Create, duplicate title for same trainer, returns 409.
    /// </summary>
    [Fact]
    public async Task Create_DuplicateTitleForSameTrainer_Returns409()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        const string title = "A Duplicated Title";
        await CreateTrainingAsync(client, title);

        var response = await client.PostAsJsonAsync("/Training", ValidCreation(title));

        // Title uniqueness per trainer is the one rule the aggregate cannot settle alone: it
        // crosses the whole stack, from IUniquenessTitleChecker down to the unique index.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Create, same title for another trainer, returns 201.
    /// </summary>
    [Fact]
    public async Task Create_SameTitleForAnotherTrainer_Returns201()
    {
        var title = $"Shared title {Guid.NewGuid():N}"[..30];

        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        (await client.PostAsJsonAsync("/Training", ValidCreation(title))).EnsureSuccessStatusCode();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // The rule is scoped to a trainer, not global: the unique index is on (TrainerId, Title).
        // A rule tightened to the title alone would still refuse this, and would be wrong.
        var response = await otherClient.PostAsJsonAsync("/Training", ValidCreation(title));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Create, no token, returns 401.
    /// </summary>
    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- Reads --

    /// <summary>
    /// Get by id, existing, returns 200 with ETag.
    /// </summary>
    [Fact]
    public async Task GetById_Existing_Returns200WithETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull("an edit needs the version this read served");
        var dto = await response.Content.ReadFromJsonAsync<TrainingHttpResponse>();
        dto!.Id.Should().Be(trainingId);
    }

    /// <summary>
    /// Get by id, non existent, returns 404.
    /// </summary>
    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -- Edit --

    /// <summary>
    /// Edit, as owner, returns 200 with the updated training and its new ETag.
    /// </summary>
    [Fact]
    public async Task Edit_AsOwner_Returns200WithTheUpdatedTrainingAndItsNewETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");
        var response = await client.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition("Renamed Training"), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The updated representation, with the new version in the ETag. The CQRS host used to
        // answer a bare 200 with neither — so a caller who edited twice in a row was guaranteed a
        // 412 on the second attempt, holding a version the first edit had just superseded, with no
        // way forward but another GET.
        var edited = await response.Content.ReadFromJsonAsync<TrainingHttpResponse>();
        edited!.Title.Should().Be("Renamed Training");

        response.Headers.ETag.Should().NotBeNull("the caller needs the new version to edit again");
        response.Headers.ETag!.ToString().Should().NotBe(
            entityTag, "the version it replaces is no longer current");
    }

    /// <summary>
    /// Edit, without if match, returns 428.
    /// </summary>
    [Fact]
    public async Task Edit_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", ValidEdition());

        // An unconditional write would let the caller overwrite changes they never saw.
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    /// <summary>
    /// Edit, with stale if match, returns 412 and keeps the first edit.
    /// </summary>
    [Fact]
    public async Task Edit_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync($"/Training/{trainingId}");

        var first = await client.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition("First Edit Wins"), staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition("Second Edit Lost"), staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetFromJsonAsync<TrainingHttpResponse>($"/Training/{trainingId}");
        reread!.Title.Should().Be("First Edit Wins", "the second edit must not have overwritten the first");
    }

    /// <summary>
    /// Edit, as non owner, returns 403.
    /// </summary>
    /// <remarks>
    /// With a valid <c>If-Match</c>, deliberately. Without one the request is refused for the
    /// missing precondition, and the 403 would prove only that authorization runs before that check
    /// — it would still pass with the ownership rule deleted.
    /// <para>
    /// The version is read by the owner, because the intruder can no longer obtain it: the read by
    /// identifier answers them 404. Handing them a version they could not have fetched is what
    /// keeps this about the write policy rather than about the read that precedes it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Edit_AsNonOwner_Returns403()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner);
        var entityTag = await owner.GetETagAsync($"/Training/{trainingId}");

        var intruder = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await intruder.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition(), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -- Delete --

    /// <summary>
    /// Delete, as owner, returns 204.
    /// </summary>
    [Fact]
    public async Task Delete_AsOwner_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The status says accepted; this says gone. A delete that answers 204 and keeps the row is
        // the failure this endpoint is most likely to have.
        (await client.GetAsync($"/Training/{trainingId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Delete, as non owner, returns 403.
    /// </summary>
    [Fact]
    public async Task Delete_AsNonOwner_Returns403()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner);

        var intruder = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await intruder.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Refused and still there: a 403 that had already deleted the row would be worse than a 204.
        (await owner.GetAsync($"/Training/{trainingId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    // -- Publish and unpublish (ADR 0050) --

    /// <summary>
    /// Create, a new training, is published.
    /// </summary>
    [Fact]
    public async Task Create_ANewTraining_IsPublished()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var training = await ReadTrainingAsync(client, trainingId);

        training.Status.Should().Be("Published");
    }

    /// <summary>
    /// Unpublish, as owner, returns 204 and the training stays readable to its owner.
    /// </summary>
    /// <remarks>
    /// The claim that separates withdrawing from deleting, over HTTP: the 204 is the same, and what
    /// follows is not. A withdrawn training answers 200 to its owner with its status changed, where
    /// a deleted one answers 404.
    /// </remarks>
    [Fact]
    public async Task Unpublish_AsOwner_Returns204AndTheTrainingSurvives()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PostAsync($"/Training/{trainingId}/unpublish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadTrainingAsync(client, trainingId)).Status.Should().Be("Unpublished");
    }

    /// <summary>
    /// Unpublish, then publish, puts the training back on offer.
    /// </summary>
    [Fact]
    public async Task Unpublish_ThenPublish_PutsTheTrainingBackOnOffer()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        (await client.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/Training/{trainingId}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadTrainingAsync(client, trainingId)).Status.Should().Be("Published");
    }

    /// <summary>
    /// Unpublish, an already withdrawn training, returns 409.
    /// </summary>
    [Fact]
    public async Task Unpublish_AnAlreadyWithdrawnTraining_Returns409()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);
        (await client.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsync($"/Training/{trainingId}/unpublish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Publish, an already published training, returns 409.
    /// </summary>
    [Fact]
    public async Task Publish_AnAlreadyPublishedTraining_Returns409()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PostAsync($"/Training/{trainingId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Unpublish, as non owner, returns 403 and changes nothing.
    /// </summary>
    [Fact]
    public async Task Unpublish_AsNonOwner_Returns403()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner);

        var intruder = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await intruder.PostAsync($"/Training/{trainingId}/unpublish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadTrainingAsync(owner, trainingId)).Status.Should().Be("Published");
    }

    /// <summary>
    /// Unpublish, a training that does not exist, returns 404.
    /// </summary>
    [Fact]
    public async Task Unpublish_ANonExistentTraining_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsync($"/Training/{Guid.NewGuid()}/unpublish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Unpublish, a training at the catalog limit, frees a place for another.
    /// </summary>
    /// <remarks>
    /// The invariant with the widest blast radius in ADR 0050, end to end: the quota counts what is
    /// on offer, so a full catalog is one a trainer can still work in. Before this, ten trainings
    /// ended a trainer's catalog for ever unless they destroyed one.
    /// </remarks>
    [Fact]
    public async Task Unpublish_AtTheCatalogLimit_FreesAPlace()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var first = await CreateTrainingAsync(client);
        for (var i = 1; i < Training.MaximumPerTrainer; i++)
        {
            await CreateTrainingAsync(client);
        }

        // Full: the eleventh is refused.
        (await client.PostAsJsonAsync("/Training", ValidCreation())).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsync($"/Training/{first}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A place freed without a row destroyed.
        (await client.PostAsJsonAsync("/Training", ValidCreation())).StatusCode
            .Should().Be(HttpStatusCode.Created);

        // And the withdrawn one cannot come back while the catalog is full again — the hole a
        // quota on published trainings would otherwise leave open.
        (await client.PostAsync($"/Training/{first}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Unpublish, a training, keeps its title taken.
    /// </summary>
    [Fact]
    public async Task Unpublish_ATraining_KeepsItsTitleTaken()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var title = $"Withdrawn {Guid.NewGuid():N}"[..25];
        var trainingId = await CreateTrainingAsync(client, title);

        (await client.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Taken, but by something its owner can see, republish or rename — so the refusal names
        // something actionable rather than something invisible (ADR 0050).
        (await client.PostAsJsonAsync("/Training", ValidCreation(title))).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<TrainingHttpResponse> ReadTrainingAsync(HttpClient client, Guid trainingId)
    {
        var response = await client.GetAsync($"/Training/{trainingId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TrainingHttpResponse>())!;
    }
}
