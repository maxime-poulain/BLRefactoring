using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.Queries;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Authorization;

/// <summary>
/// What decides <see cref="ActiveTrainerPolicy"/>, on the paths no request can take.
/// </summary>
/// <remarks>
/// The two ordinary answers — suspended and active — are proven end to end on both hosts by
/// <c>SuspendedTrainerTest</c>. What only this suite can reach is what the handler does when the
/// request is not a request at all: no <c>HttpContext</c>, and a caller whose token carries no
/// <c>trainer_id</c>. Both are decisions rather than accidents, and both are silent in production —
/// a handler that failed the requirement for a missing context would refuse every write of the
/// surface, and nothing would say why.
/// </remarks>
public sealed class ActiveTrainerAuthorizationHandlerTests
{
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private readonly RecordingStandingQuery _standing = new();

    /// <summary>
    /// Handle, a suspended trainer, does not grant the requirement.
    /// </summary>
    [Fact]
    public async Task Handle_ASuspendedTrainer_DoesNotGrantTheRequirement()
    {
        var trainerId = Guid.NewGuid();
        Calling(trainerId);
        Suspended(trainerId, suspended: true);

        var context = await HandleAsync();

        context.HasSucceeded.Should().BeFalse();
    }

    /// <summary>
    /// Handle, a trainer in good standing, grants the requirement.
    /// </summary>
    [Fact]
    public async Task Handle_ATrainerInGoodStanding_GrantsTheRequirement()
    {
        var trainerId = Guid.NewGuid();
        Calling(trainerId);
        Suspended(trainerId, suspended: false);

        var context = await HandleAsync();

        context.HasSucceeded.Should().BeTrue();
    }

    /// <summary>
    /// Handle, no request at all, grants nothing and asks nothing.
    /// </summary>
    /// <remarks>
    /// Neither succeeding nor failing: the handler abstains, which leaves the requirement unmet and
    /// the request refused. The port is asserted untouched because reaching the database to decide
    /// about a caller who does not exist is the mistake the abstention prevents.
    /// </remarks>
    [Fact]
    public async Task Handle_NoRequestAtAll_GrantsNothingAndAsksNothing()
    {
        _httpContextAccessor.HttpContext = null;

        var context = await HandleAsync();

        context.HasSucceeded.Should().BeFalse();

        _standing.WasAsked.Should().BeFalse();
    }

    /// <summary>
    /// Handle, a caller who is nobody's trainer, grants the requirement and asks nothing.
    /// </summary>
    /// <remarks>
    /// An administrator carries no <c>trainer_id</c>, so this policy has nothing to say about them:
    /// <see cref="TrainerPolicy"/> already refuses them on every action of the surface, reads
    /// included. Failing here as well would answer the same caller twice, and would make a second
    /// policy responsible for a refusal that is not its own (ADR 0051).
    /// </remarks>
    [Fact]
    public async Task Handle_ACallerWhoIsNobodysTrainer_GrantsTheRequirementAndAsksNothing()
    {
        _httpContextAccessor.HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        var context = await HandleAsync();

        context.HasSucceeded.Should().BeTrue();

        _standing.WasAsked.Should().BeFalse();
    }

    /// <summary>
    /// Handle, a claim that is not an identifier, grants the requirement and asks nothing.
    /// </summary>
    /// <remarks>
    /// The same abstention as an absent claim, for the same reason: what a token carries is the
    /// authentication layer's business, and a handler that threw on a malformed one would answer
    /// <c>500</c> where the surface already answers <c>403</c>.
    /// </remarks>
    [Fact]
    public async Task Handle_AClaimThatIsNotAnIdentifier_GrantsTheRequirementAndAsksNothing()
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(TrainerClaims.TrainerId, "not an identifier")]))
        };

        var context = await HandleAsync();

        context.HasSucceeded.Should().BeTrue();

        _standing.WasAsked.Should().BeFalse();
    }

    private void Calling(Guid trainerId) =>
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(TrainerClaims.TrainerId, trainerId.ToString())]))
        };

    private void Suspended(Guid trainerId, bool suspended) => _standing.Answer(trainerId, suspended);

    private async Task<AuthorizationHandlerContext> HandleAsync()
    {
        var requirement = new ActiveTrainerRequirement();

        var context = new AuthorizationHandlerContext(
            [requirement], new ClaimsPrincipal(new ClaimsIdentity()), resource: null);

        await new ActiveTrainerAuthorizationHandler(_httpContextAccessor, _standing)
            .HandleAsync(context);

        return context;
    }

    /// <remarks>
    /// Hand-written rather than mocked, like <c>StubTrainingOwnerQuery</c> beside it: this project
    /// carries no mocking library, and what these facts need from the port is one answer and the
    /// knowledge of whether it was asked at all.
    /// </remarks>
    private sealed class RecordingStandingQuery : ITrainerStandingQuery
    {
        private Guid _trainerId;
        private bool _suspended;

        public bool WasAsked { get; private set; }

        public void Answer(Guid trainerId, bool suspended)
        {
            _trainerId = trainerId;
            _suspended = suspended;
        }

        public Task<bool> IsSuspendedAsync(Guid trainerId, CancellationToken cancellationToken = default)
        {
            WasAsked = true;

            return Task.FromResult(trainerId == _trainerId && _suspended);
        }
    }
}
