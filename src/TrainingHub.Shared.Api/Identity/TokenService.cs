using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TrainingHub.Shared.Application.Queries;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// Interface defining the contract for a token service.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user and their roles.
    /// </summary>
    /// <param name="user">The user for whom the token is generated.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A JWT token as a string.</returns>
    Task<string> GenerateTokenAsync(IdentityUser<Guid> user, IList<string> roles, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the ITokenService interface for generating JWT tokens.
/// </summary>
/// <remarks>
/// The clock arrives as a <see cref="TimeProvider"/>, like everywhere else in this solution that
/// needs one — the audit interceptor already did. It used to read <c>DateTime.Now</c>: correct by
/// accident, since <c>EpochTime.GetIntDate</c> converts a local kind before writing <c>exp</c>,
/// and untestable on purpose by nobody. A token's lifetime is a security property; it should be
/// possible to assert it without waiting.
/// <para>
/// An account with no trainer used to be refused a token outright. That guard clause was the one
/// thing standing between this product and an administrator, who is an account and not a trainer
/// (ADR 0051): they have no profile to publish, no bio and no contact address, and giving them one
/// would be a lie in the data model. The three trainer claims are simply omitted, and the trainer
/// surface refuses such a token at the boundary through <c>TrainerPolicy</c>.
/// </para>
/// </remarks>
public sealed class TokenService(
    IConfiguration configuration,
    ITrainerIdentityQuery trainerIdentityQuery,
    TimeProvider timeProvider) : ITokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user and their roles.
    /// </summary>
    /// <param name="user">The user for whom the token is generated.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A JWT token as a string.</returns>
    public async Task<string> GenerateTokenAsync(IdentityUser<Guid> user, IList<string> roles, CancellationToken cancellationToken = default)
    {
        // Retrieve JWT settings from the configuration.
        var jwtSettings = configuration.GetSection("Jwt");

        // Create a symmetric security key using the secret key from the configuration.
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSettings["Key"]!));

        // Define the signing credentials using the security key and HMAC-SHA256 algorithm.
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var trainer = await trainerIdentityQuery.GetByUserIdAsync(user.Id, cancellationToken);

        // Define the claims for the token, including user information and roles.
        var claims = new List<Claim>
        {
            new (ClaimTypes.Name, user.UserName!),
            new (ClaimTypes.NameIdentifier, user.Id.ToString()),
            new (ClaimTypes.Email, user.Email!)
        };

        // Absent rather than empty when the account is nobody's trainer. An empty trainer_id would
        // satisfy every claim check that only asks whether the claim is there, and then fail far
        // away from here, inside a Guid.Parse.
        if (trainer is not null)
        {
            claims.Add(new Claim("firstname", trainer.Firstname));
            claims.Add(new Claim("lastname", trainer.Lastname));
            claims.Add(new Claim(TrainerClaims.TrainerId, trainer.TrainerId.ToString()));
        }

        // Add role claims for each role assigned to the user.
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Create the JWT token with the specified issuer, audience, claims, expiration, and signing credentials.
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: timeProvider.GetUtcNow()
                .AddMinutes(double.Parse(jwtSettings["ExpireMinutes"]!, CultureInfo.InvariantCulture))
                .UtcDateTime,
            signingCredentials: creds);

        // Return the serialized JWT token as a string.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
