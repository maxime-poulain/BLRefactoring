using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The catalog's reading of one offering trainer over HTTP, on both hosts (ADR 0070).
/// </summary>
/// <remarks>
/// The sibling of <see cref="CatalogDetailTest{TFactory}"/>, for the page a visitor reaches from
/// the one that suite proves. What only a real request through a real database can show is the
/// composition: the index answers whether this person may be looked at and what they offer, the
/// write model answers who they are, and a moderation decision moves the whole page without either
/// endpoint reading a status.
/// <para>
/// Every assertion that waits for the index polls, for the reason the search suite gives at
/// length: the index is eventually consistent by design, and that is the property rather than a
/// nuisance.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogTrainerProfileTest<TFactory>(TFactory factory)
    : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
{
    /// <summary>
    /// An offering trainer, is read by a caller with no token, and the training's page links here.
    /// </summary>
    /// <remarks>
    /// Both halves of the navigation in one fact: the detail hands out the trainer's identifier,
    /// and the identifier resolves to a page carrying the name, the bio and the offered list. A
    /// profile without the first half is unreachable, and a detail without the second links to a
    /// 404.
    /// </remarks>
    [Fact]
    public async Task AnOfferingTrainer_IsReadByACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var trainingId = await PublishAsync(trainer, "Reading One Offering Trainer");
        await WaitUntilOfferedAsync(trainingId);

        var anonymous = Factory.CreateClient();

        var detail = await anonymous.GetFromJsonAsync<CatalogTrainingDetailHttpResponse>(
            $"/Catalog/trainings/{trainingId}");

        detail!.TrainerId.Should().Be(me.Id,
            "the training's page has to offer an address the profile below will answer");

        var read = await anonymous.GetAsync($"/Catalog/trainers/{detail.TrainerId}");

        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await read.Content.ReadFromJsonAsync<CatalogTrainerHttpResponse>();

        profile.Should().NotBeNull();
        profile!.Id.Should().Be(me.Id);
        profile.Firstname.Should().Be(me.Firstname);
        profile.Lastname.Should().Be(me.Lastname);
        profile.Bio.Should().Be(me.Bio);
        profile.Trainings.Should().ContainSingle(offered =>
            offered.Id == trainingId
            && offered.TrainerId == me.Id
            && offered.Title == "Reading One Offering Trainer");
    }

    /// <summary>
    /// The offered list, is alphabetical and holds only what is on offer.
    /// </summary>
    /// <remarks>
    /// The profile is a shelf of the same catalog: the order is the search's own (ADR 0001,
    /// ADR 0029), and a withdrawn training is as absent here as it is there.
    /// </remarks>
    [Fact]
    public async Task TheOfferedList_IsAlphabeticalAndOnlyWhatIsOnOffer()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var zebra = await PublishAsync(trainer, "Zebra Patterns In Production");
        var agile = await PublishAsync(trainer, "Agile Architecture Explained");
        var withdrawn = await PublishAsync(trainer, "Withdrawn Before Anybody Looked");

        await WaitUntilOfferedAsync(zebra);
        await WaitUntilOfferedAsync(agile);
        await WaitUntilOfferedAsync(withdrawn);

        (await trainer.PostAsync($"/Training/{withdrawn}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = Factory.CreateClient();
        var titles = await WaitForOfferedTitlesAsync(anonymous, me.Id, expectedCount: 2);

        titles.Should().Equal("Agile Architecture Explained", "Zebra Patterns In Production");
    }

    /// <summary>
    /// A suspension takes the page down, and a reinstatement brings it back.
    /// </summary>
    /// <remarks>
    /// The whole page moves on one flag neither endpoint reads: the sanction flips the index's
    /// column through its consumer, and the profile follows the trainer's catalog out of public
    /// view and back into it (ADR 0056, ADR 0070). During the sanction the 404 is
    /// indistinguishable from anybody's, which is what "offered or invisible" costs on purpose.
    /// </remarks>
    [Fact]
    public async Task ASuspension_TakesThePageDown_AndAReinstatementBringsItBack()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var trainingId = await PublishAsync(trainer, "A Page That Follows Its Owner");
        await WaitUntilOfferedAsync(trainingId);

        var anonymous = Factory.CreateClient();

        (await WaitForProfileStatusAsync(anonymous, me.Id, HttpStatusCode.OK))
            .Should().Be(HttpStatusCode.OK);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
                $"/Administration/trainers/{me.Id}/suspend",
                new SuspendTrainerHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForProfileStatusAsync(anonymous, me.Id, HttpStatusCode.NotFound))
            .Should().Be(HttpStatusCode.NotFound);

        (await administrator.PostAsync($"/Administration/trainers/{me.Id}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForProfileStatusAsync(anonymous, me.Id, HttpStatusCode.OK))
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A trainer with nothing on offer, answers the same nothing as a person who never existed.
    /// </summary>
    /// <remarks>
    /// Offered or invisible, over HTTP: the trainer is registered, their row is perfectly
    /// readable, and withdrawing their one training is what takes the page down. A 404 carrying a
    /// difference would tell an anonymous caller that a person exists and is not presentable,
    /// which is the administration's read (ADR 0055, ADR 0070).
    /// </remarks>
    [Fact]
    public async Task ATrainerWithNothingOnOffer_AnswersTheSameNothingAsNobody()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var trainingId = await PublishAsync(trainer, "The Only Training On The Shelf");
        await WaitUntilOfferedAsync(trainingId);

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = Factory.CreateClient();

        (await WaitForProfileStatusAsync(anonymous, me.Id, HttpStatusCode.NotFound))
            .Should().Be(HttpStatusCode.NotFound);

        var neverExisted = await anonymous.GetAsync($"/Catalog/trainers/{Guid.CreateVersion7()}");
        neverExisted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The empty identifier, is refused rather than answered with a nothing.
    /// </summary>
    /// <remarks>
    /// A malformed message and an absent person are different answers, and this endpoint has to
    /// give both (ADR 0046).
    /// </remarks>
    [Fact]
    public async Task TheEmptyIdentifier_IsRefusedRatherThanAnsweredWithANothing()
    {
        var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync($"/Catalog/trainers/{Guid.Empty}");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// An offering trainer's portrait, is served to a caller with no token, at the profile's own
    /// address.
    /// </summary>
    /// <remarks>
    /// The per-person twin of the per-training portrait (ADR 0070): the profile names the photo,
    /// the address built from the two answers forever-cacheable bytes, and an identity the trainer
    /// does not have answers nothing — which is what keeps <c>immutable</c> honest.
    /// </remarks>
    [Fact]
    public async Task AnOfferingTrainersPortrait_IsServedToACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = await UploadPortraitAsync(trainer);

        var trainingId = await PublishAsync(trainer, "A Profile With A Face");
        await WaitUntilOfferedAsync(trainingId);

        var anonymous = Factory.CreateClient();

        var profile = await anonymous.GetFromJsonAsync<CatalogTrainerHttpResponse>(
            $"/Catalog/trainers/{me.Id}");

        profile!.PhotoId.Should().Be(me.PhotoId,
            "the page has to offer an address the endpoint below will answer");

        var portrait = await anonymous.GetAsync(
            $"/Catalog/trainers/{me.Id}/photo/{profile.PhotoId}");

        portrait.StatusCode.Should().Be(HttpStatusCode.OK);
        portrait.Content.Headers.ContentType!.MediaType.Should().Be(TrainerPhoto.PngContentType);

        (await portrait.Content.ReadAsByteArrayAsync())
            .Take(8)
            .Should().Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        portrait.Headers.CacheControl!.Extensions
            .Select(extension => extension.Name)
            .Should().Contain("immutable");

        var moved = await anonymous.GetAsync(
            $"/Catalog/trainers/{me.Id}/photo/{Guid.CreateVersion7()}");

        moved.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Publishes a portrait on the caller's own profile, and answers their profile.</summary>
    private static async Task<TrainerHttpResponse> UploadPortraitAsync(HttpClient trainer)
    {
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(Portraits.Of(TrainerPhoto.PngContentType));

        part.Headers.ContentType = new MediaTypeHeaderValue(TrainerPhoto.PngContentType);
        form.Add(part, "photo", "portrait");

        (await trainer.PutAsync("/Trainer/me/photo", form))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");

        me!.PhotoId.Should().NotBeNull();

        return me;
    }

    /// <summary>Creates and publishes a training, and answers its identifier.</summary>
    private static async Task<Guid> PublishAsync(HttpClient trainer, string title)
    {
        var created = await trainer.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Polls the catalog's search until the index holds the training.</summary>
    /// <remarks>
    /// The search rather than the profile, on purpose: waiting on the endpoint under test would
    /// make a permanently broken profile look like a slow index.
    /// </remarks>
    private async Task WaitUntilOfferedAsync(Guid trainingId)
    {
        var anonymous = Factory.CreateClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var page = await anonymous.GetAsync("/Catalog/trainings?page=1&pageSize=50");
            page.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await page.Content.ReadFromJsonAsync<JsonElement>();

            if (body.GetProperty("items").EnumerateArray()
                .Any(item => item.GetProperty("id").GetGuid() == trainingId))
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    /// <summary>Polls the profile until it answers the status expected, or gives up and answers.</summary>
    private static async Task<HttpStatusCode> WaitForProfileStatusAsync(
        HttpClient anonymous,
        Guid trainerId,
        HttpStatusCode expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var last = HttpStatusCode.Unused;

        while (DateTime.UtcNow < deadline)
        {
            last = (await anonymous.GetAsync($"/Catalog/trainers/{trainerId}")).StatusCode;

            if (last == expected)
            {
                return last;
            }

            await Task.Delay(100);
        }

        return last;
    }

    /// <summary>Polls the profile until its list reaches the size expected, and answers the titles.</summary>
    private static async Task<IReadOnlyList<string>> WaitForOfferedTitlesAsync(
        HttpClient anonymous,
        Guid trainerId,
        int expectedCount)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        IReadOnlyList<string> titles = [];

        while (DateTime.UtcNow < deadline)
        {
            var read = await anonymous.GetAsync($"/Catalog/trainers/{trainerId}");

            if (read.StatusCode == HttpStatusCode.OK)
            {
                var profile = await read.Content.ReadFromJsonAsync<CatalogTrainerHttpResponse>();
                titles = profile!.Trainings.Select(offered => offered.Title).ToList();

                if (titles.Count == expectedCount)
                {
                    return titles;
                }
            }

            await Task.Delay(100);
        }

        return titles;
    }
}
