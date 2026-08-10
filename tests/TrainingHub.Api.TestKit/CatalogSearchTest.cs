using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The public catalog over HTTP, on both hosts: what a visitor with no token can find, and what
/// the write side has to do before they can (ADR 0059).
/// </summary>
/// <remarks>
/// The only suite here that proves the whole chain of the Search Indexing context end to end: a
/// write commits its fact, the delivery worker replays it into the index after the commit
/// (ADR 0002, ADR 0025), and an anonymous read answers from the index rather than from the
/// trainings table. Everything in between is exercised elsewhere against substitutes; none of it
/// proves that a visitor eventually sees anything.
/// <para>
/// Every assertion polls, because the index is eventually consistent by design and that is the
/// property under test rather than a nuisance to work around. The polling shape is
/// <c>OutboxTest.WaitForMessageAsync</c>'s.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogSearchTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
{
    /// <summary>
    /// A published training, becomes findable by a word of its title, to a caller with no token.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose: every other read of this API is refused <c>401</c> without one, and the
    /// difference is the whole point of the fourth controller base. The term is one word of the
    /// title rather than the title itself, because matching a whole string would pass against an
    /// index that had never been tokenized.
    /// </remarks>
    [Fact]
    public async Task APublishedTraining_BecomesFindableByAWordOfItsTitle_ToACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Hexagonal Architecture Explained"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("hexagonal", holds: trainingId)).Should().Contain(trainingId);
    }

    /// <summary>
    /// A withdrawn training, leaves the catalog.
    /// </summary>
    /// <remarks>
    /// The defect ADR 0050 named from the other end: without the removal, an index would go on
    /// serving a training its owner has taken back, and the state would be a lie the write side
    /// alone could not detect.
    /// </remarks>
    [Fact]
    public async Task AWithdrawnTraining_LeavesTheCatalog()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Withdrawn Before Long"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("withdrawn", holds: trainingId)).Should().Contain(trainingId);

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("withdrawn", holdsNot: trainingId)).Should().NotContain(trainingId);
    }

    /// <summary>
    /// A suspended trainer's catalog, leaves public view, and comes back when the sanction is
    /// lifted.
    /// </summary>
    /// <remarks>
    /// The end of the chain ADR 0056 designed, asserted where it matters: not on a column, but on
    /// what somebody outside the product can see. The lifting is half the fact — an index that had
    /// deleted the entries would need the catalog read back to rebuild them, which is precisely
    /// the design that record rejected.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainersCatalog_LeavesPublicView_AndComesBackWhenTheSanctionIsLifted()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainerId = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!.Id;

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Sanctioned Catalog Entry"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("sanctioned", holds: trainingId)).Should().Contain(trainingId);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
                $"/Administration/trainers/{trainerId}/suspend",
                new SuspendTrainerHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("sanctioned", holdsNot: trainingId)).Should().NotContain(trainingId);

        (await administrator.PostAsync(
                $"/Administration/trainers/{trainerId}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("sanctioned", holds: trainingId)).Should().Contain(trainingId);
    }

    /// <summary>
    /// A term longer than a title, is refused rather than answered with an empty page.
    /// </summary>
    /// <remarks>
    /// An empty page is a legitimate answer to a good question, so it must not also be the answer to
    /// a bad one — the reading ADR 0055 gives an unknown status, applied to a term.
    /// </remarks>
    [Fact]
    public async Task ATermLongerThanATitle_IsRefusedRatherThanAnsweredWithAnEmptyPage()
    {
        var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync(
            $"/Catalog/trainings?term={new string('a', 101)}");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Polls the public catalog until it holds — or stops holding — the training, and answers the
    /// identifiers of the page it last read.
    /// </summary>
    /// <remarks>
    /// Exactly one of the two expectations is given by each caller; the other is
    /// <see langword="null"/>. Answering the page rather than a boolean is what lets the caller
    /// assert in its own words and read a useful failure when the index never converged.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> WaitForCatalogAsync(
        string term,
        Guid? holds = null,
        Guid? holdsNot = null)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        var anonymous = Factory.CreateClient();
        IReadOnlyList<Guid> lastSeen = [];

        while (DateTime.UtcNow - started < timeout)
        {
            var page = await anonymous.GetAsync($"/Catalog/trainings?term={term}&page=1&pageSize=50");
            page.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await page.Content.ReadFromJsonAsync<JsonElement>();
            lastSeen = [.. body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())];

            if ((holds is null || lastSeen.Contains(holds.Value))
                && (holdsNot is null || !lastSeen.Contains(holdsNot.Value)))
            {
                return lastSeen;
            }

            await Task.Delay(100);
        }

        return lastSeen;
    }
}
