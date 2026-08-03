using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// The authorization policies an API host enforces.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the <see cref="TrainingOwnerPolicy"/> and the handler that decides it.
    /// </summary>
    /// <remarks>
    /// Registering the requirement and its handler together is the point: a policy without its
    /// handler never succeeds, and a handler without its policy is never consulted. Kept as one
    /// call so a host cannot end up with half of the pair.
    /// </remarks>
    public static IServiceCollection AddTrainingOwnerAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAuthorizationHandler, TrainingOwnerAuthorizationHandler>();

        return services.AddAuthorization(options =>
        {
            options.AddPolicy(TrainingOwnerPolicy.Name, policy =>
                policy.Requirements.Add(new TrainingOwnerRequirement()));
        });
    }
}
