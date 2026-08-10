using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using TrainingHub.Shared.Api.Identity;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Identity;

/// <summary>
/// Behavior covered for <c>CurrentUserService</c>: which caller each request resolves to, and what
/// happens when it resolves to nobody.
/// </summary>
/// <remarks>
/// The absent claim is the case worth pinning. Since ADR 0051 there is a caller who genuinely has
/// no trainer — an administrator — and <c>TrainerPolicy</c> refuses them before any action runs.
/// This accessor therefore keeps raising rather than answering, and the distinction matters: a
/// request that reaches here without the claim is a policy that was not applied, which is a
/// configuration fault, not a caller error. The tempting repair — answering
/// <see cref="Guid.Empty"/> — would turn it into a caller who silently owns nothing and edits
/// nobody's profile.
/// </remarks>
public sealed class CurrentUserServiceTests
{
    /// <summary>
    /// A trainer's request, resolves to the trainer the claim names.
    /// </summary>
    [Fact]
    public void ATrainersRequest_ResolvesToTheTrainerTheClaimNames()
    {
        var trainerId = Guid.NewGuid();

        var service = For(new Claim(TrainerClaims.TrainerId, trainerId.ToString()));

        service.TrainerId.Should().Be(trainerId);
    }

    /// <summary>
    /// A request with no trainer claim, raises rather than answering.
    /// </summary>
    [Fact]
    public void ARequestWithNoTrainerClaim_RaisesRatherThanAnswering()
    {
        var service = For(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var reading = () => service.TrainerId;

        reading.Should().Throw<InvalidOperationException>()
            .WithMessage("*no trainer_id claim*");
    }

    /// <summary>
    /// A signed-in caller, resolves to their identity account.
    /// </summary>
    /// <remarks>
    /// The one an administrator does have, and the one ADR 0027's enricher stamps on every line
    /// they cause.
    /// </remarks>
    [Fact]
    public void ASignedInCaller_ResolvesToTheirIdentityAccount()
    {
        var userId = Guid.NewGuid();

        var service = For(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        service.UserId.Should().Be(userId);
    }

    private static CurrentUserService For(params Claim[] claims) =>
        new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        });
}
