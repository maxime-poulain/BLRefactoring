using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.Queries;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Authorization;

/// <summary>
/// Behaviour covered for <c>AddApiAuthorization</c>: the three policies both hosts enforce, and
/// what each of them demands.
/// </summary>
/// <remarks>
/// A policy that is never registered is not reported at start-up: ASP.NET Core discovers it is
/// missing when a request arrives at the action that names it, and answers 500. That makes the
/// registration exactly the kind of thing worth asserting away from the wire — and it is the only
/// way to assert <see cref="AdministratorPolicy"/> at all until the first administrative endpoint
/// exists, since a policy nothing carries is never evaluated.
/// </remarks>
public sealed class PolicyRegistrationTests
{
    /// <summary>
    /// The three policies, are all registered.
    /// </summary>
    /// <remarks>
    /// One call registers all three so a host cannot hold two of them, which is what this asserts:
    /// the names, together, from one <c>AddApiAuthorization</c>.
    /// </remarks>
    [Fact]
    public async Task TheThreePolicies_AreAllRegistered()
    {
        var provider = PolicyProvider();

        var policies = await Task.WhenAll(
            provider.GetPolicyAsync(TrainingOwnerPolicy.Name),
            provider.GetPolicyAsync(TrainerPolicy.Name),
            provider.GetPolicyAsync(AdministratorPolicy.Name));

        policies.Should().NotContainNulls();
    }

    /// <summary>
    /// The administrator policy, demands the administrator role.
    /// </summary>
    /// <remarks>
    /// The role rather than merely "some requirement": the string that reaches
    /// <c>RequireRole</c> is what a token has to carry, and a policy demanding a role nobody is
    /// ever granted refuses everybody — which looks exactly like a policy that works.
    /// </remarks>
    [Fact]
    public async Task TheAdministratorPolicy_DemandsTheAdministratorRole()
    {
        var policy = await PolicyProvider().GetPolicyAsync(AdministratorPolicy.Name);

        policy!.Requirements
            .OfType<RolesAuthorizationRequirement>()
            .SelectMany(requirement => requirement.AllowedRoles)
            .Should().Equal(IdentityRoles.Administrator);
    }

    /// <summary>
    /// The trainer policy, demands the trainer claim.
    /// </summary>
    /// <remarks>
    /// The claim and not its value: the policy asks whether the caller is somebody's trainer, and
    /// which trainer they are is the ownership policy's question.
    /// </remarks>
    [Fact]
    public async Task TheTrainerPolicy_DemandsTheTrainerClaim()
    {
        var policy = await PolicyProvider().GetPolicyAsync(TrainerPolicy.Name);

        policy!.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Select(requirement => requirement.ClaimType)
            .Should().Equal(TrainerClaims.TrainerId);
    }

    /// <summary>
    /// The ownership policy, carries the handler that decides it.
    /// </summary>
    /// <remarks>
    /// The half a policy cannot state about itself. Its requirement is empty by design — ownership
    /// is a database question — so the policy is worth nothing without the handler, and the two are
    /// registered by one call precisely so neither can arrive alone.
    /// </remarks>
    [Fact]
    public void TheOwnershipPolicy_CarriesTheHandlerThatDecidesIt() =>
        Services().BuildServiceProvider()
            .GetServices<IAuthorizationHandler>()
            .Should().ContainSingle(handler => handler is TrainingOwnerAuthorizationHandler);

    /// <summary>
    /// The standing policy, carries the handler that decides it.
    /// </summary>
    /// <remarks>
    /// The second policy whose question only the database can answer, and the second that fails
    /// silently without its handler: a requirement nothing decides never succeeds, so every write of
    /// the trainer surface would answer <c>403</c> to everybody (ADR 0053).
    /// </remarks>
    [Fact]
    public void TheStandingPolicy_CarriesTheHandlerThatDecidesIt() =>
        Services().BuildServiceProvider()
            .GetServices<IAuthorizationHandler>()
            .Should().ContainSingle(handler => handler is ActiveTrainerAuthorizationHandler);

    /// <summary>
    /// The standing policy, demands a trainer who is not suspended.
    /// </summary>
    [Fact]
    public async Task TheStandingPolicy_DemandsATrainerWhoIsNotSuspended()
    {
        var policy = await PolicyProvider().GetPolicyAsync(ActiveTrainerPolicy.Name);

        policy!.Requirements.Should().ContainSingle().Which.Should().BeOfType<ActiveTrainerRequirement>();
    }

    private sealed class StubTrainerStandingQuery : ITrainerStandingQuery
    {
        public Task<bool> IsSuspendedAsync(Guid trainerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static IAuthorizationPolicyProvider PolicyProvider() =>
        Services().BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

    /// <remarks>
    /// Logging because <c>AddAuthorization</c>'s own services ask for it, and the ownership
    /// handler's two dependencies because resolving <c>IAuthorizationHandler</c> constructs it.
    /// Nothing else of the host is present, which is the point: this asserts the registration and
    /// not a pipeline.
    /// </remarks>
    private static IServiceCollection Services() =>
        new ServiceCollection()
            .AddLogging()
            .AddHttpContextAccessor()
            .AddSingleton<ITrainingOwnerQuery, StubTrainingOwnerQuery>()
            .AddSingleton<ITrainerStandingQuery, StubTrainerStandingQuery>()
            .AddApiAuthorization();

    private sealed class StubTrainingOwnerQuery : ITrainingOwnerQuery
    {
        public Task<Guid?> GetOwnerIdAsync(Guid trainingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }
}
