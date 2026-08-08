using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.Queries;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Identity;

/// <summary>
/// Behaviour covered for <c>TokenService</c>: which claims a token carries, and which it does not.
/// </summary>
/// <remarks>
/// The interesting half is the account that is nobody's trainer. Issuing a token to one used to be
/// refused outright, which is what stood between this product and an administrator (ADR 0051): an
/// administrator is an account, not a trainer, and giving them a publishable profile to satisfy a
/// guard clause would be a lie in the data model.
/// </remarks>
public sealed class TokenServiceTests
{
    /// <summary>
    /// An account with no trainer, receives a token whose trainer claim is absent.
    /// </summary>
    /// <remarks>
    /// Absent rather than empty, and that is the assertion worth making twice: an empty
    /// <c>trainer_id</c> satisfies every check that only asks whether the claim is there —
    /// <c>TrainerPolicy</c> among them — and then fails far away, inside a <c>Guid.Parse</c>.
    /// </remarks>
    [Fact]
    public async Task AnAccountWithNoTrainer_ReceivesATokenThatNamesNone()
    {
        var token = await IssueAsync(trainer: null);

        token.Claims.Select(claim => claim.Type).Should()
            .NotContain([TrainerClaims.TrainerId, "firstname", "lastname"]);
    }

    /// <summary>
    /// An account with no trainer, is still named and still carries its roles.
    /// </summary>
    /// <remarks>
    /// The other half of the same change: dropping the three trainer claims must not drop the
    /// caller's identity with them, or the administrator becomes anonymous to the logging enricher
    /// of ADR 0027 — which is the only record of who acted.
    /// </remarks>
    [Fact]
    public async Task AnAccountWithNoTrainer_IsStillNamedAndStillCarriesItsRoles()
    {
        var token = await IssueAsync(trainer: null, roles: ["Administrator"]);

        Value(token, ClaimTypes.Name).Should().Be("ada.lovelace");
        Value(token, ClaimTypes.Email).Should().Be("ada@example.com");
        Value(token, ClaimTypes.Role).Should().Be("Administrator");
    }

    /// <summary>
    /// A trainer's account, carries the three claims the trainer surface reads.
    /// </summary>
    [Fact]
    public async Task ATrainersAccount_CarriesTheThreeTrainerClaims()
    {
        var trainerId = Guid.NewGuid();

        var token = await IssueAsync(new TrainerIdentityDto(trainerId, "Ada", "Lovelace"));

        Value(token, TrainerClaims.TrainerId).Should().Be(trainerId.ToString());
        Value(token, "firstname").Should().Be("Ada");
        Value(token, "lastname").Should().Be("Lovelace");
    }

    private static async Task<JwtSecurityToken> IssueAsync(
        TrainerIdentityDto? trainer,
        IList<string>? roles = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "a-key-long-enough-for-hmac-sha256-to-accept-it",
                ["Jwt:Issuer"] = "TrainingHub.Api",
                ["Jwt:Audience"] = "TrainingHub.Web",
                ["Jwt:ExpireMinutes"] = "60"
            })
            .Build();

        // The real clock: nothing here asserts a lifetime, and a frozen one at the epoch would only
        // make every token expired before it is read.
        var service = new TokenService(
            configuration,
            new StubTrainerIdentityQuery(trainer),
            TimeProvider.System);

        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(),
            UserName = "ada.lovelace",
            Email = "ada@example.com"
        };

        return new JwtSecurityTokenHandler()
            .ReadJwtToken(await service.GenerateTokenAsync(user, roles ?? []));
    }

    private static string? Value(JwtSecurityToken token, string claimType) =>
        token.Claims.SingleOrDefault(claim => claim.Type == claimType)?.Value;

    /// <summary>
    /// Answers whatever the test handed it, including nothing.
    /// </summary>
    /// <remarks>
    /// Written out rather than mocked: the interface has one method, and this suite carries no
    /// mocking library — one it would acquire for six lines.
    /// </remarks>
    private sealed class StubTrainerIdentityQuery(TrainerIdentityDto? trainer) : ITrainerIdentityQuery
    {
        public Task<TrainerIdentityDto?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(trainer);
    }
}
