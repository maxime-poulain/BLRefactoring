using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// ASP.NET Identity and everything that reads the identity of the caller.
/// </summary>
public static class IdentityExtensions
{
    /// <summary>
    /// Registers the identity store, the sign-in rules, token issuance and the accessor both
    /// hosts use to resolve the calling trainer.
    /// </summary>
    /// <remarks>
    /// The password and lockout rules are deliberately part of the shared call: they are the
    /// security posture of the product, not of one host, and two copies of them are two chances
    /// to tighten only one.
    /// <para>
    /// <c>AddHttpContextAccessor</c> is called explicitly even though <c>AddIdentity</c> registers
    /// it as a side effect: both <see cref="CurrentUserService"/> and the training-ownership
    /// handler depend on it, and one of the two hosts was relying on that side effect without ever
    /// asking for it — a dependency that would have broken the day Identity changed.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApiIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<TrainingIdentityDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("TrainingContext"));
        });

        services.AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>(options =>
            {
                // Deliberately absent, and a rule keeps it that way: SignIn.RequireConfirmedEmail
                // and SignIn.RequireConfirmedAccount must never be set here. An unverified account
                // signs in, manages its space and reads everything — only the catalog write asks
                // for the proof — and the one-line "hardening" either flag offers would silently
                // invert that business rule (ADR 0090).
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<TrainingIdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();

        return services
            .AddScoped<ITokenService, TokenService>()
            .AddScoped<ICurrentUserService, CurrentUserService>()
            // The recovery flow of ADR 0084 — a shared Identity service in TokenService's mold,
            // so both hosts publish the same two endpoints from one implementation.
            .AddScoped<IPasswordRecoveryService, PasswordRecoveryService>()
            // The verification flow of ADR 0090, in the recovery service's mold and for its
            // reason: one implementation behind both hosts' verify and resend endpoints.
            .AddScoped<IEmailVerificationService, EmailVerificationService>();
    }
}
