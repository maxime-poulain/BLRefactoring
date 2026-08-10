using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The two reads that survive answer with the caller's trainings and with nobody else's.
/// </summary>
/// <remarks>
/// Run against both hosts, because the endpoints exist on both and the two reach them by different
/// routes: the layered host asks its application service for the trainer the token names, while the
/// CQRS host dispatches a query whose <c>Where</c> is composed into SQL. Two implementations of one
/// promise, and only a test crossing both can say they keep it.
/// <para>
/// Two endpoints are covered here, because the API's promise is about the pair rather than either.
/// <c>GET /Training/my-trainings</c> takes no identifier, so isolation is structural; but the list
/// hands out identifiers, and <c>GET /Training/{id}</c> takes one, so scoping the list while leaving
/// the read by identifier open would hide nothing. The four catalog reads that used to sit beside
/// them are gone; these two are what remain, and both are asserted.
/// </para>
/// <para>
/// The assertions are made on the raw body rather than on a deserialized type, and deliberately.
/// They state a claim about leakage, and leakage does not care which property it travels in: a
/// title anywhere in the response is a leak, and deserializing would narrow the search to the
/// fields the type happens to declare. Both hosts answer the same page envelope since ADR 0029 —
/// <c>PaginationTest</c> holds the shared paging assertions — but these stay on the string,
/// because absence from the whole body is the stronger claim.
/// </para>
/// <para>
/// Isolation is what this proves, so it needs two trainers. <c>AuthHelper</c> registers a fresh one
/// per call, and each client carries its own token.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class OwnTrainingsTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    // Short on purpose: CreateTrainingHttpRequest.Title is [StringLength(100, MinimumLength = 5)],
    // so a sentence-length title is refused at model binding and the POST that sets the scene fails
    // before the read under test is ever reached. Neither is a substring of the other, which is what
    // the assertions below rely on.
    private const string Mine = "My own training";
    private const string Theirs = "Another trainer's";

    /// <summary>
    /// Own trainings, contains mine, and not somebody elses.
    /// </summary>
    [Fact]
    public async Task OwnTrainings_ContainsMine_AndNotSomebodyElses()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var theirs = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await mine.PostAsJsonAsync("/Training", TrainingRequests.Valid(Mine)))
            .EnsureSuccessStatusCode();
        (await theirs.PostAsJsonAsync("/Training", TrainingRequests.Valid(Theirs)))
            .EnsureSuccessStatusCode();

        var response = await mine.GetAsync("/Training/my-trainings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(Mine, "the caller's own training is what the endpoint is for");
        body.Should().NotContain(
            Theirs,
            "another trainer's training must not reach the caller at all — hiding it in the client " +
            "would leave it in the response, where anyone can read it");
    }

    /// <summary>
    /// Own trainings, takes no identifier, so the caller cannot ask for another.
    /// </summary>
    [Fact]
    public async Task OwnTrainings_TakesNoIdentifier_SoTheCallerCannotAskForAnother()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var theirs = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await theirs.PostAsJsonAsync("/Training", TrainingRequests.Valid(Theirs)))
            .EnsureSuccessStatusCode();

        // The endpoint binds nothing, so a query string naming somebody else is not a parameter —
        // it is noise. The answer must be the same as without it: this caller's trainings, which
        // here is none. The point is that there is no input to tamper with; the endpoint that did
        // take a trainer identifier is the one this replaced.
        var response = await mine.GetAsync("/Training/my-trainings?trainerId=" + Guid.NewGuid());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync()).Should().NotContain(Theirs);
    }

    /// <summary>
    /// Own trainings, requires authentication.
    /// </summary>
    [Fact]
    public async Task OwnTrainings_RequiresAuthentication()
    {
        var anonymous = Factory.CreateClient();

        // ApiControllerBase carries [Authorize] for every action, so this is refused before the
        // action runs. Asserted rather than assumed: the endpoint returns data scoped by a token,
        // and an unauthenticated call has no scope to be given.
        var response = await anonymous.GetAsync("/Training/my-trainings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Training by id, belonging to another trainer, is not found.
    /// </summary>
    [Fact]
    public async Task TrainingById_BelongingToAnotherTrainer_IsNotFound()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var theirs = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var creation = await theirs.PostAsJsonAsync("/Training", TrainingRequests.Valid(Theirs));
        creation.EnsureSuccessStatusCode();
        var trainingId = await creation.Content.ReadFromJsonAsync<Guid>();

        // 404 rather than 403, and the distinction is the point: a 403 would confirm that this
        // identifier names a real training, which is itself the thing being withheld. The caller
        // cannot tell an identifier that belongs to somebody else from one that belongs to nobody.
        var response = await mine.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(Theirs);
    }

    /// <summary>
    /// Training by id, belonging to the caller, is served.
    /// </summary>
    [Fact]
    public async Task TrainingById_BelongingToTheCaller_IsServed()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var creation = await mine.PostAsJsonAsync("/Training", TrainingRequests.Valid(Mine));
        creation.EnsureSuccessStatusCode();
        var trainingId = await creation.Content.ReadFromJsonAsync<Guid>();

        // The other half of the guard, and the half that breaks if the ownership clause is written
        // wrong: a filter that matches nothing turns every read into a 404, the edit form never
        // loads, and the suite above would not notice because it only ever asserts absence.
        var response = await mine.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(Mine);
    }
}
