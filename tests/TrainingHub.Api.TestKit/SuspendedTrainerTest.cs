using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// What a suspended trainer may and may not do over HTTP, on both hosts (ADR 0053, ADR 0057).
/// </summary>
/// <remarks>
/// The decision is a table — every read kept, every write refused — and a table is the kind of claim
/// that decays one endpoint at a time. The architecture rule holds the policy on every write; only
/// this suite can say that the policy, the handler, the read port and the database agree.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class SuspendedTrainerTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    private const string Motive = "Repeated breaches of the content policy.";

    /// <summary>
    /// A suspended trainer, is refused every write of their own surface.
    /// </summary>
    /// <remarks>
    /// All nine, because the sanction is about the surface rather than about a chosen endpoint. The
    /// body is asserted empty on each: a policy refusal carries no document, and a caller who met a
    /// problem document here would be meeting the aggregate instead of the boundary.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainer_IsRefusedEveryWriteOfTheirOwnSurface()
    {
        var (trainer, trainingId) = await SuspendedWithATrainingAsync();

        var writes = new (string Verb, string Route)[]
        {
            ("PUT", "/Trainer/me"),
            ("PUT", "/Trainer/me/photo"),
            ("DELETE", "/Trainer/me/photo"),
            ("POST", "/Training"),
            ("PUT", $"/Training/{trainingId}"),
            ("DELETE", $"/Training/{trainingId}"),
            ("POST", $"/Training/{trainingId}/transfer"),
            ("POST", $"/Training/{trainingId}/publish"),
            ("POST", $"/Training/{trainingId}/unpublish")
        };

        foreach (var (verb, route) in writes)
        {
            var refused = await trainer.SendAsync(
                new HttpRequestMessage(new HttpMethod(verb), route));

            refused.StatusCode.Should().Be(
                HttpStatusCode.Forbidden, "{0} {1} is a write of the trainer surface", verb, route);

            (await refused.Content.ReadAsStringAsync()).Should().BeEmpty();
        }
    }

    /// <summary>
    /// A suspended trainer's unpublish, is refused too.
    /// </summary>
    /// <remarks>
    /// Its own fact because ADR 0050 explicitly permitted it, which makes it the one a future reader
    /// will assume still works. It is the sentence ADR 0053 amends, and this is where the amendment
    /// stops being prose.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainersUnpublish_IsRefusedToo()
    {
        var (trainer, trainingId) = await SuspendedWithATrainingAsync();

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A suspended trainer, keeps every read.
    /// </summary>
    /// <remarks>
    /// Not a softening of the sanction: it is what makes the sanction accountable, and it is the
    /// half a policy on the controller base would have taken away in one line.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainer_KeepsEveryRead()
    {
        var (trainer, trainingId) = await SuspendedWithATrainingAsync();

        foreach (var route in new[] { "/Trainer/me", "/Training/my-trainings", $"/Training/{trainingId}" })
        {
            (await trainer.GetAsync(route)).StatusCode
                .Should().Be(HttpStatusCode.OK, "{0} is a read", route);
        }
    }

    /// <summary>
    /// A suspended trainer, reads their standing and the reason for it.
    /// </summary>
    /// <remarks>
    /// The reason is minted per run, so a fact that passed against a hard-coded sentence would fail
    /// here. It is the administrator's words, unedited — two texts describing one sanction is how a
    /// product ends up arguing with itself (ADR 0057).
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainer_ReadsTheirStandingAndItsReason()
    {
        var motive = $"Reported for misleading claims on {Guid.NewGuid()}.";
        var (trainer, _) = await SuspendedWithATrainingAsync(motive);

        var profile = await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");

        profile!.Status.Should().Be("Suspended");
        profile.SuspensionReason.Should().Be(motive);
    }

    /// <summary>
    /// A withheld training, tells its owner why.
    /// </summary>
    /// <remarks>
    /// The consequence ADR 0052 named and ADR 0055 deferred, discharged end to end: a state the
    /// owner's interface renders as merely "not published" would hide the one thing they need to
    /// know.
    /// </remarks>
    [Fact]
    public async Task AWithheldTraining_TellsItsOwnerWhy()
    {
        var motive = $"Plagiarised material, case {Guid.NewGuid()}.";

        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(trainer);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = motive }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var training = await trainer.GetFromJsonAsync<TrainingHttpResponse>($"/Training/{trainingId}");

        training!.Status.Should().Be("Withheld");
        training.WithholdingReason.Should().Be(motive);
    }

    /// <summary>
    /// A reinstated trainer, writes again without signing in again.
    /// </summary>
    /// <remarks>
    /// The fact that proves the standing is read rather than carried. Had it become a claim, the
    /// same token would still say <c>Active</c> after a suspension and <c>Suspended</c> after a
    /// lifting, and this would be the test that never went red.
    /// </remarks>
    [Fact]
    public async Task AReinstatedTrainer_WritesAgainWithoutSigningInAgain()
    {
        var (trainer, trainingId) = await SuspendedWithATrainingAsync();
        var trainerId = await CurrentTrainerIdAsync(trainer);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsync(
            $"/Administration/trainers/{trainerId}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<(HttpClient Trainer, Guid TrainingId)> SuspendedWithATrainingAsync(
        string motive = Motive)
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(trainer);
        var trainerId = await CurrentTrainerIdAsync(trainer);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{trainerId}/suspend",
            new SuspendTrainerHttpRequest { Reason = motive }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (trainer, trainingId);
    }

    private static async Task<Guid> CreateTrainingAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("A training its owner made before the sanction"));

        created.EnsureSuccessStatusCode();

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> CurrentTrainerIdAsync(HttpClient client)
    {
        var profile = await client.GetAsync("/Trainer/me");
        profile.EnsureSuccessStatusCode();

        return (await profile.Content.ReadFromJsonAsync<TrainerHttpResponse>())!.Id;
    }
}
