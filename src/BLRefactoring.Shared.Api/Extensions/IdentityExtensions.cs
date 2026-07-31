using BLRefactoring.Shared.Api.Identity;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BLRefactoring.Shared.Api.Extensions;

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
            .AddScoped<ICurrentUserService, CurrentUserService>();
    }
}
