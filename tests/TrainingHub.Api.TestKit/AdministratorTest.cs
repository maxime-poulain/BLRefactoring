using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Identity;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// What an account that is nobody's trainer can and cannot do.
/// </summary>
/// <remarks>
/// An administrator is an account, not a trainer (ADR 0051). Two things follow, and both are
/// easier to get wrong than to state: a token can be issued without a <c>trainer_id</c>, and the
/// trainer surface has to refuse such a token rather than raise on the missing claim. The second is
/// the one that decays silently — a <c>500</c> where a <c>403</c> belongs still looks like a
/// refusal from the outside.
/// <para>
/// Both directions of <see cref="AdministratorPolicy"/> are here too, now that there are endpoints
/// behind it: what the policy admits and what it refuses. A requirement nobody satisfies refuses
/// everybody, which passes every test that only checks for refusals — so the pass matters as much
/// as the two rejections. What those endpoints *do* is <see cref="ModerationTest{TFactory}"/>'s.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class AdministratorTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    /// <summary>
    /// An account with no trainer, signs in and receives a token that names no trainer.
    /// </summary>
    /// <remarks>
    /// Absent rather than empty. An empty <c>trainer_id</c> would satisfy the trainer policy and
    /// then fail inside the first <c>Guid.Parse</c> that reads it, which is a 500 two layers away
    /// from its cause.
    /// </remarks>
    [Fact]
    public async Task AnAccountWithNoTrainer_SignsInAndReceivesATokenThatNamesNoTrainer()
    {
        var administrator = await AuthHelper.CreateAdministratorAsync(Factory);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await AuthHelper.LoginAsync(Factory.CreateClient(), administrator.Username, administrator.Password));

        token.Claims.Select(claim => claim.Type).Should().NotContain(TrainerClaims.TrainerId);
        token.Claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.Role && claim.Value == IdentityRoles.Administrator);
    }

    /// <summary>
    /// An administrator, is refused the trainer surface.
    /// </summary>
    /// <remarks>
    /// Three actions across both controllers, chosen so that a read, a write and a creation are
    /// each represented: the policy sits on the shared base, so if it holds it holds for all
    /// thirteen, and if somebody moves it onto individual actions this notices.
    /// </remarks>
    [Fact]
    public async Task AnAdministrator_IsRefusedTheTrainerSurface()
    {
        var client = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await client.GetAsync("/Trainer/me")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        // Fully populated, so the refusal cannot be mistaken for a rejected body — authorization
        // runs before model binding, and a reader should not have to know that to trust the fact.
        var edition = new EditTrainerHttpRequest
        {
            Firstname = "Ada",
            Lastname = "Lovelace",
            ContactEmail = "ada@example.com"
        };

        (await client.PutAsJsonAsync("/Trainer/me", edition)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await client.PostAsJsonAsync("/Training", TrainingRequests.Valid())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// An anonymous caller, is still refused before the policy is reached.
    /// </summary>
    /// <remarks>
    /// The regression the new policy could quietly cause: a policy that carries no explicit
    /// <c>RequireAuthenticatedUser</c> must still leave the 401 to the authentication middleware,
    /// or every unauthenticated call starts answering 403 and clients stop knowing to sign in.
    /// </remarks>
    [Fact]
    public async Task AnAnonymousCaller_IsStillRefusedWithUnauthorized() =>
        (await Factory.CreateClient().GetAsync("/Trainer/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

    /// <summary>
    /// A trainer, still reaches their own profile.
    /// </summary>
    /// <remarks>
    /// The other side of the same policy, and the reason it is worth asserting at all: a
    /// requirement nobody satisfies refuses everybody, which passes every test that only checks
    /// for refusals.
    /// </remarks>
    [Fact]
    public async Task ATrainer_StillReachesTheirOwnProfile()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await client.GetAsync("/Trainer/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A trainer, is refused the administrative surface.
    /// </summary>
    /// <remarks>
    /// The other half of the split ADR 0051 forced, and the one worth watching: a trainer's token
    /// is a perfectly good token, so nothing about it fails authentication. What refuses it is the
    /// role it does not hold, which is why the answer is 403 and not 401.
    /// <para>
    /// A body that would be accepted, and a trainer that exists — their own — so the refusal cannot
    /// be mistaken for a rejected payload or a missing row. Authorization runs before model binding
    /// and before any handler, and a reader should not have to know that to trust the fact.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ATrainer_IsRefusedTheAdministrativeSurface()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var profile = await client.GetAsync("/Trainer/me");
        profile.EnsureSuccessStatusCode();
        var trainerId = (await profile.Content.ReadFromJsonAsync<TrainerHttpResponse>())!.Id;

        var suspension = new SuspendTrainerHttpRequest { Reason = "A reason the domain would accept." };

        (await client.PostAsJsonAsync($"/Administration/trainers/{trainerId}/suspend", suspension))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await client.PostAsync($"/Administration/trainers/{trainerId}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// An anonymous caller, is refused the administrative surface with unauthorized.
    /// </summary>
    /// <remarks>
    /// The same regression as on the trainer surface, and it has to be asserted on this base
    /// separately: the two carry different policies, and a policy that spelled out
    /// <c>RequireAuthenticatedUser</c> would turn every unauthenticated call into a 403 and stop
    /// clients knowing to sign in.
    /// </remarks>
    [Fact]
    public async Task AnAnonymousCaller_IsRefusedTheAdministrativeSurfaceWithUnauthorized() =>
        (await Factory.CreateClient().PostAsync(
            $"/Administration/trainings/{Guid.NewGuid()}/release", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
