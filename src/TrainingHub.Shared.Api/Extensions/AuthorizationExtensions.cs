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
    /// Registers the five policies both hosts publish, and the three handlers they need.
    /// </summary>
    /// <remarks>
    /// Registering the requirements and their handlers together is the point: a policy without its
    /// handler never succeeds, and a handler without its policy is never consulted. Kept as one
    /// call so a host cannot end up with half of a pair — nor with four of the five policies.
    /// <para>
    /// Three of them need a handler of their own, because their questions are ones only the
    /// database can answer: who owns a training, whether a trainer is under suspension, and
    /// whether the account has proven its address. The other two are decided from the token the
    /// caller already presented, and the framework's own requirements read it.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAuthorizationHandler, TrainingOwnerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveTrainerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, VerifiedTrainerAuthorizationHandler>();

        return services.AddAuthorization(options =>
        {
            options.AddPolicy(TrainingOwnerPolicy.Name, policy =>
                policy.Requirements.Add(new TrainingOwnerRequirement()));

            // Named at each write rather than carried by ApiControllerBase: a suspended trainer
            // keeps every read, so a policy on the base would take their own profile and catalog
            // away from them — the alternative ADR 0053 rejected by name.
            options.AddPolicy(ActiveTrainerPolicy.Name, policy =>
                policy.Requirements.Add(new ActiveTrainerRequirement()));

            // Named at exactly one action — creating a training — because that is the only door
            // verification guards: an unverified trainer can never already own a training, so
            // every other write is out of their reach by construction (ADR 0090).
            options.AddPolicy(VerifiedTrainerPolicy.Name, policy =>
                policy.Requirements.Add(new VerifiedTrainerRequirement()));

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
