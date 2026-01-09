using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;

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
public sealed class TokenService(IConfiguration configuration, ITrainerRepository trainerRepository) : ITokenService
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

        var trainer = await trainerRepository.GetByUserIdAsync((UserId)user.Id, cancellationToken);

        if (trainer is null)
        {
            throw new ApplicationException("Invalid trainer id");
        }

        // Define the claims for the token, including user information and roles.
        var claims = new List<Claim>
        {
            new (ClaimTypes.Name, user.UserName!),
            new (ClaimTypes.NameIdentifier, user.Id.ToString()),
            new (ClaimTypes.Email, user.Email!),
            new ("firstname", trainer.Name.Firstname),
            new ("lastname", trainer.Name.Lastname),
            new ("trainer_id", trainer.Id.Value.ToString())
        };

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
            expires: DateTime.Now.AddMinutes(double.Parse(jwtSettings["ExpireMinutes"]!, CultureInfo.InvariantCulture)),
            signingCredentials: creds);

        // Return the serialized JWT token as a string.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
