using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// <c>GET /Training/me</c> answers with the caller's trainings and with nobody else's.
/// </summary>
/// <remarks>
/// Run against both hosts, because the endpoint exists on both and the two reach it by different
/// routes: the layered host asks its application service for the trainer the token names, while the
/// CQRS host dispatches a query whose <c>Where</c> is composed into SQL. Two implementations of one
/// promise, and only a test crossing both can say they keep it.
/// <para>
/// The assertions are made on the raw body rather than on a deserialised type, and deliberately.
/// The two hosts answer different shapes — a bare array here, a page envelope there — so a shared
/// test that bound to either would be testing one host and compiling against the other. What both
/// must satisfy is stated in the only vocabulary they share: the title of a training is in the
/// response, or it is not.
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
    // Short on purpose: CreateTrainingRequestHttp.Title is [StringLength(100, MinimumLength = 5)],
    // so a sentence-length title is refused at model binding and the POST that sets the scene fails
    // before the read under test is ever reached. Neither is a substring of the other, which is what
    // the assertions below rely on.
    private const string Mine = "My own training";
    private const string Theirs = "Another trainer's";

    [Fact]
    public async Task OwnTrainings_ContainsMine_AndNotSomebodyElses()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var theirs = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await mine.PostAsJsonAsync("/Training", TrainingRequests.Valid(Mine)))
            .EnsureSuccessStatusCode();
        (await theirs.PostAsJsonAsync("/Training", TrainingRequests.Valid(Theirs)))
            .EnsureSuccessStatusCode();

        var response = await mine.GetAsync("/Training/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(Mine, "the caller's own training is what the endpoint is for");
        body.Should().NotContain(
            Theirs,
            "another trainer's training must not reach the caller at all — hiding it in the client " +
            "would leave it in the response, where anyone can read it");
    }

    [Fact]
    public async Task OwnTrainings_TakesNoIdentifier_SoTheCallerCannotAskForAnother()
    {
        var mine = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var theirs = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await theirs.PostAsJsonAsync("/Training", TrainingRequests.Valid(Theirs)))
            .EnsureSuccessStatusCode();

        // The endpoint binds nothing, so a query string naming somebody else is not a parameter —
        // it is noise. The answer must be the same as without it: this caller's trainings, which
        // here is none. The point is that there is no input to tamper with, unlike by-trainer/{id}.
        var response = await mine.GetAsync("/Training/me?trainerId=" + Guid.NewGuid());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadAsStringAsync()).Should().NotContain(Theirs);
    }

    [Fact]
    public async Task OwnTrainings_RequiresAuthentication()
    {
        var anonymous = Factory.CreateClient();

        // ApiControllerBase carries [Authorize] for every action, so this is refused before the
        // action runs. Asserted rather than assumed: the endpoint returns data scoped by a token,
        // and an unauthenticated call has no scope to be given.
        var response = await anonymous.GetAsync("/Training/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
