using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The four administrative decisions over HTTP, on both hosts.
/// </summary>
/// <remarks>
/// The transitions are the aggregate's and are proven in the domain suite; the orchestration is
/// proven twice in the two application suites. What only this one can prove is the claim ADR 0052
/// exists to make, which no unit test reaches: <em>the owner cannot undo a withholding</em>. That
/// takes two callers, two tokens and two controllers in the same run — the administrator moving the
/// state, the owner meeting both closed doors, and the same owner publishing freely once the
/// interdiction is lifted.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class ModerationTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    private const string Motive = "Reported for misleading claims about certification.";

    /// <summary>
    /// A withheld training, is one its owner cannot put back by either door.
    /// </summary>
    /// <remarks>
    /// Both doors, because refusing only <c>publish</c> would leave the interdiction liftable in
    /// two moves that each pass a check — the shape of the quota bypass ADR 0050 found, and the
    /// thing ADR 0052's design did not say until it was built.
    /// </remarks>
    [Fact]
    public async Task AWithheldTraining_IsOneItsOwnerCannotPutBackByEitherDoor()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner, "A training the administration will withhold");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var withheld = await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = Motive });

        withheld.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var republished = await owner.PostAsync($"/Training/{trainingId}/publish", content: null);
        republished.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ShouldCarryAsync(republished, TrainingErrorCodes.Withheld);

        var withdrawn = await owner.PostAsync($"/Training/{trainingId}/unpublish", content: null);
        withdrawn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ShouldCarryAsync(withdrawn, TrainingErrorCodes.Withheld);
    }

    /// <summary>
    /// A released training, is the owner's to publish again.
    /// </summary>
    /// <remarks>
    /// Releasing lands on unpublished, never on published: the administration lifts an interdiction,
    /// it does not decide to put a training back in the window. What proves the landing state is
    /// this second request succeeding — publishing something already published would answer 409.
    /// </remarks>
    [Fact]
    public async Task AReleasedTraining_IsTheOwnersToPublishAgain()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner, "A training the administration will release");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = Motive }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await administrator.PostAsync($"/Administration/trainings/{trainingId}/release", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.PostAsync($"/Training/{trainingId}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A second withholding, is refused as a conflict.
    /// </summary>
    [Fact]
    public async Task ASecondWithholding_IsRefusedAsAConflict()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner, "A training withheld twice over");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);
        var body = new WithholdTrainingHttpRequest { Reason = Motive };

        (await administrator.PostAsJsonAsync($"/Administration/trainings/{trainingId}/withhold", body))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold", body);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ShouldCarryAsync(again, TrainingErrorCodes.AlreadyWithheld);
    }

    /// <summary>
    /// Releasing a training nobody withheld, is refused as a conflict.
    /// </summary>
    [Fact]
    public async Task ReleasingATrainingNobodyWithheld_IsRefusedAsAConflict()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner, "A training nobody ever withheld");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var released = await administrator.PostAsync(
            $"/Administration/trainings/{trainingId}/release", content: null);

        released.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ShouldCarryAsync(released, TrainingErrorCodes.NotWithheld);
    }

    /// <summary>
    /// A withholding with no reason, is refused before the domain is reached.
    /// </summary>
    /// <remarks>
    /// The boundary's own refusal, keyed by field, because a sanction without a stated motive is the
    /// one thing ADR 0052 forbids and the caller should learn which field is at fault. The domain
    /// refuses it too, on its own terms — the two answer different callers (ADR 0043).
    /// </remarks>
    [Fact]
    public async Task AWithholdingWithNoReason_IsRefusedBeforeTheDomainIsReached()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner, "A training withheld for no stated reason");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var refused = await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{trainingId}/withhold",
            new WithholdTrainingHttpRequest { Reason = string.Empty });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement
            .GetProperty("errors")
            .TryGetProperty(nameof(WithholdTrainingHttpRequest.Reason), out _)
            .Should().BeTrue("the caller is told which field is at fault");
    }

    /// <summary>
    /// Withholding a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task WithholdingATrainingThatDoesNotExist_AnswersNotFound()
    {
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var refused = await administrator.PostAsJsonAsync(
            $"/Administration/trainings/{Guid.NewGuid()}/withhold",
            new WithholdTrainingHttpRequest { Reason = Motive });

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A suspended trainer, comes back exactly as they left.
    /// </summary>
    /// <remarks>
    /// The sanction writes one field, and lifting it writes the same one back (ADR 0050): nothing
    /// is cascaded onto the trainer's trainings, so nothing has to be restored. The training
    /// unpublished before the sanction is still unpublished after it, which is the scenario that
    /// decided the shape of that record.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainer_ComesBackExactlyAsTheyLeft()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var offered = await CreateTrainingAsync(trainer, "A training still on offer");
        var withdrawn = await CreateTrainingAsync(trainer, "A training its owner withdrew");

        (await trainer.PostAsync($"/Training/{withdrawn}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var trainerId = await CurrentTrainerIdAsync(trainer);
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{trainerId}/suspend",
            new SuspendTrainerHttpRequest { Reason = Motive }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Suspended, the trainer's catalogue cannot grow again — and the refusal now arrives one
        // layer earlier than it used to. ADR 0053 moved it to the boundary, so the request is turned
        // away by ActiveTrainerPolicy before any use case runs, with no body at all. The aggregate's
        // own standing rule is still there and still tested, by the domain suite and by both
        // application suites, which reach the use case without passing a policy: it is defence in
        // depth now rather than the only door.
        var refused = await trainer.PostAsync($"/Training/{withdrawn}/publish", content: null);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().BeEmpty(
            "a policy refusal carries no document, which is what tells this one apart from the "
            + "aggregate's");

        (await administrator.PostAsync($"/Administration/trainers/{trainerId}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Nothing was restored, because nothing was taken: the one the trainer withdrew is still
        // theirs to publish, and the one that was on offer never left it.
        (await trainer.PostAsync($"/Training/{withdrawn}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await trainer.PostAsync($"/Training/{offered}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A second suspension, is refused as a conflict.
    /// </summary>
    [Fact]
    public async Task ASecondSuspension_IsRefusedAsAConflict()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainerId = await CurrentTrainerIdAsync(trainer);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);
        var body = new SuspendTrainerHttpRequest { Reason = Motive };

        (await administrator.PostAsJsonAsync($"/Administration/trainers/{trainerId}/suspend", body))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{trainerId}/suspend", body);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ShouldCarryAsync(again, TrainerErrorCodes.AlreadySuspended);
    }

    /// <summary>
    /// Reinstating a trainer nobody suspended, is refused as a conflict.
    /// </summary>
    [Fact]
    public async Task ReinstatingATrainerNobodySuspended_IsRefusedAsAConflict()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainerId = await CurrentTrainerIdAsync(trainer);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var refused = await administrator.PostAsync(
            $"/Administration/trainers/{trainerId}/reinstate", content: null);

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ShouldCarryAsync(refused, TrainerErrorCodes.NotSuspended);
    }

    /// <summary>
    /// Suspending a trainer that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task SuspendingATrainerThatDoesNotExist_AnswersNotFound()
    {
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var refused = await administrator.PostAsJsonAsync(
            $"/Administration/trainers/{Guid.NewGuid()}/suspend",
            new SuspendTrainerHttpRequest { Reason = Motive });

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Creates a training for the given caller and answers its identifier.
    /// </summary>
    private static async Task<Guid> CreateTrainingAsync(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));
        created.EnsureSuccessStatusCode();

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Reads the calling trainer's own identifier off their profile.
    /// </summary>
    /// <remarks>
    /// The administrative routes name a trainer, and registration answers the identifier only in
    /// the <c>Location</c> of a resource the caller must be signed in to read. Asking
    /// <c>/Trainer/me</c> is what the caller themselves would do, and it keeps this helper free of
    /// any assumption about how the identifier reached the client.
    /// </remarks>
    private static async Task<Guid> CurrentTrainerIdAsync(HttpClient client)
    {
        var profile = await client.GetAsync("/Trainer/me");
        profile.EnsureSuccessStatusCode();

        return (await profile.Content.ReadFromJsonAsync<TrainerHttpResponse>())!.Id;
    }

    /// <summary>
    /// Asserts that a refusal carries the named domain code, in the shape ADR 0004 requires.
    /// </summary>
    private static async Task ShouldCarryAsync(HttpResponseMessage response, ErrorCode code)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(code.Value);
    }
}
