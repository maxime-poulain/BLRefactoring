using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// The authorization policies an API host enforces.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers the three policies both hosts publish, and the handler the first of them needs.
    /// </summary>
    /// <remarks>
    /// Registering the requirement and its handler together is the point: a policy without its
    /// handler never succeeds, and a handler without its policy is never consulted. Kept as one
    /// call so a host cannot end up with half of the pair — and, now that there are three policies,
    /// so it cannot end up with two of the three either.
    /// <para>
    /// Only <see cref="TrainingOwnerPolicy"/> needs a handler of its own, because ownership is a
    /// question only the database can answer. The other two are decided from the token the caller
    /// already presented, and the framework's own requirements read it.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAuthorizationHandler, TrainingOwnerAuthorizationHandler>();

        return services.AddAuthorization(options =>
        {
            options.AddPolicy(TrainingOwnerPolicy.Name, policy =>
                policy.Requirements.Add(new TrainingOwnerRequirement()));

            // The trainer surface, carried by ApiControllerBase rather than named at each action:
            // an account with no trainer — an administrator — must meet a 403 and not the 500 that
            // reading an absent claim would produce. See TrainerPolicy for why the refusal belongs
            // here and not in ICurrentUserService.
            options.AddPolicy(TrainerPolicy.Name, policy =>
                policy.RequireClaim(TrainerClaims.TrainerId));

            options.AddPolicy(AdministratorPolicy.Name, policy =>
                policy.RequireRole(IdentityRoles.Administrator));
        });
    }
}
