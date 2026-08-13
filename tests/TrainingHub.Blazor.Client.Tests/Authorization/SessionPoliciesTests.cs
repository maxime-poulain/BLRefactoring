using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Blazor.Client.Authorization;
using TrainingHub.Blazor.Client.Extensions;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Authorization;

/// <summary>
/// The question the browser asks about its caller, evaluated for real.
/// </summary>
/// <remarks>
/// Every other fact about the trainer's doors is written against bUnit's authorization double,
/// which grants a policy by name and never looks at a claim — so without this, nothing in the
/// suite would notice a policy registered against the wrong claim, or against none. This is the
/// one place the registration itself is exercised: the real
/// <see cref="IAuthorizationService"/>, over the real policy, against a principal built the way
/// <c>TokenService</c> builds one (ADR 0051, ADR 0078).
/// </remarks>
public sealed class SessionPoliciesTests
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddLogging().AddServices().BuildServiceProvider();

    /// <summary>
    /// The trainer policy, a caller whose identity carries the claim, is granted.
    /// </summary>
    [Fact]
    public async Task TheTrainerPolicy_ACallerWhoseIdentityCarriesTheClaim_IsGranted()
    {
        var allowed = await AuthorizeAsync(new Claim(SessionClaims.TrainerId, Guid.NewGuid().ToString()));

        allowed.Should().BeTrue();
    }

    /// <summary>
    /// The trainer policy, an administrator, is refused.
    /// </summary>
    /// <remarks>
    /// Their identity carries the role and no <c>trainer_id</c>, which is exactly what the API
    /// mints for an account that is nobody's trainer — and exactly what the API's own
    /// <c>TrainerPolicy</c> refuses. Two policies, one question.
    /// </remarks>
    [Fact]
    public async Task TheTrainerPolicy_AnAdministrator_IsRefused()
    {
        var allowed = await AuthorizeAsync(new Claim(ClaimTypes.Role, SessionRoles.Administrator));

        allowed.Should().BeFalse(
            "an administrator is an account and not a trainer, and the browser asks the same " +
            "question the API asks (ADR 0051)");
    }

    /// <summary>
    /// The trainer policy, a visitor, is refused.
    /// </summary>
    [Fact]
    public async Task TheTrainerPolicy_AVisitor_IsRefused()
    {
        var authorization = Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), resource: null, SessionPolicies.Trainer);

        result.Succeeded.Should().BeFalse();
    }

    private static async Task<bool> AuthorizeAsync(params Claim[] claims)
    {
        var authorization = Services.GetRequiredService<IAuthorizationService>();

        var caller = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "bff"));

        return (await authorization.AuthorizeAsync(caller, resource: null, SessionPolicies.Trainer))
            .Succeeded;
    }
}
