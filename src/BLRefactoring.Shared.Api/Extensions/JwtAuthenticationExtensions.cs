using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BLRefactoring.Shared.Api.Extensions;

/// <summary>
/// How an API host validates the bearer tokens it is handed.
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication from the <c>Jwt</c> section of the configuration.
    /// </summary>
    /// <remarks>
    /// The signing key has no safe fallback: failing at startup with a clear message beats a
    /// <see cref="NullReferenceException"/> on the first authenticated request — and beats far
    /// more the alternative of a default key, which would validate tokens anyone could mint.
    /// <para>
    /// Sharing the validation parameters matters more than sharing the lines: were one host to
    /// lose <c>ValidateIssuer</c> or <c>ValidateLifetime</c>, it would keep accepting tokens the
    /// other rejects, and every test would still pass.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The signing key is not configured.</exception>
    public static IServiceCollection AddJwtBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var signingKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Missing configuration value 'Jwt:Key'. Provide it via appsettings, " +
                "environment variables or user secrets before starting the host.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
            };
        });

        return services;
    }
}
