using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Catalogue;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The catalogue's reading of one training over HTTP, on both hosts (ADR 0062).
/// </summary>
/// <remarks>
/// The sibling of <see cref="CatalogueSearchTest{TFactory}"/>, and it proves the half that suite
/// cannot: a search asks the index one question and answers from it, while this endpoint asks the
/// index one question and answers from the write model. Only a real request through a real database
/// shows the two halves agreeing — a unit test substitutes the port that joins them.
/// <para>
/// Every assertion that waits for the index polls, for the reason the search suite gives at length:
/// the index is eventually consistent by design, and that is the property rather than a nuisance.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogueDetailTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
{
    /// <summary>
    /// An offered training, is read in full by a caller with no token, trainer's name included.
    /// </summary>
    /// <remarks>
    /// The name is the column that makes this endpoint worth having and the one the index cannot
    /// hold, so it is what a failure here would be about. The rest of the body is asserted with it
    /// because a reading that answered only a title would be the search result again.
    /// </remarks>
    [Fact]
    public async Task AnOfferedTraining_IsReadInFullByACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var trainingId = await PublishAsync(trainer, "Reading One Offered Training");

        await WaitUntilOfferedAsync(trainingId);

        var anonymous = Factory.CreateClient();
        var read = await anonymous.GetAsync($"/Catalogue/trainings/{trainingId}");

        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var offered = await read.Content.ReadFromJsonAsync<CatalogueTrainingDetailHttpResponse>();

        offered.Should().NotBeNull();
        offered!.Id.Should().Be(trainingId);
        offered.Title.Should().Be("Reading One Offered Training");
        offered.TrainerName.Should().Be($"{me.Firstname} {me.Lastname}");
        offered.Description.Should().NotBeNullOrWhiteSpace();
        offered.Prerequisites.Should().NotBeNullOrWhiteSpace();
        offered.AcquiredSkills.Should().NotBeNullOrWhiteSpace();
        offered.Topics.Should().NotBeEmpty();
    }

    /// <summary>
    /// A withheld training, answers a visitor the same nothing as one that never existed.
    /// </summary>
    /// <remarks>
    /// The fact the whole visibility design turns on, and the one that cannot be asserted anywhere
    /// but here: the row is still in the trainings table, perfectly readable, and the only thing
    /// between it and the visitor is the index entry the moderator's decision removed (ADR 0056).
    /// A 403, or a 404 carrying a reason, would tell an anonymous caller that a training exists and
    /// has been taken down — which is the administration's read (ADR 0055).
    /// </remarks>
    [Fact]
    public async Task AWithheldTraining_AnswersTheSameNothingAsOneThatNeverExisted()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await PublishAsync(trainer, "Withheld Before A Visitor Arrived");

        await WaitUntilOfferedAsync(trainingId);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
                $"/Administration/trainings/{trainingId}/withhold",
                new WithholdTrainingHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = Factory.CreateClient();
        var withheld = await WaitForStatusAsync(anonymous, trainingId, HttpStatusCode.NotFound);

        withheld.Should().Be(HttpStatusCode.NotFound);

        // The training is still there for its owner, which is what makes the 404 above a decision
        // about the audience rather than a deletion.
        (await trainer.GetAsync($"/Training/{trainingId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var neverExisted = await anonymous.GetAsync($"/Catalogue/trainings/{Guid.CreateVersion7()}");
        neverExisted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The empty identifier, is refused rather than answered with a nothing.
    /// </summary>
    /// <remarks>
    /// A malformed message and an absent training are different answers, and this endpoint has to
    /// give both (ADR 0046).
    /// </remarks>
    [Fact]
    public async Task TheEmptyIdentifier_IsRefusedRatherThanAnsweredWithANothing()
    {
        var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync($"/Catalogue/trainings/{Guid.Empty}");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Creates and publishes a training, and answers its identifier.</summary>
    private static async Task<Guid> PublishAsync(HttpClient trainer, string title)
    {
        var created = await trainer.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Polls the catalogue's search until the index holds the training.</summary>
    /// <remarks>
    /// The search rather than the reading, on purpose: waiting on the endpoint under test would
    /// make a permanently broken reading look like a slow index.
    /// </remarks>
    private async Task WaitUntilOfferedAsync(Guid trainingId)
    {
        var anonymous = Factory.CreateClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var page = await anonymous.GetAsync("/Catalogue/trainings?page=1&pageSize=50");
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

    /// <summary>Polls the reading until it answers the status expected, or gives up and answers.</summary>
    private static async Task<HttpStatusCode> WaitForStatusAsync(
        HttpClient anonymous,
        Guid trainingId,
        HttpStatusCode expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var last = HttpStatusCode.Unused;

        while (DateTime.UtcNow < deadline)
        {
            last = (await anonymous.GetAsync($"/Catalogue/trainings/{trainingId}")).StatusCode;

            if (last == expected)
            {
                return last;
            }

            await Task.Delay(100);
        }

        return last;
    }
}
