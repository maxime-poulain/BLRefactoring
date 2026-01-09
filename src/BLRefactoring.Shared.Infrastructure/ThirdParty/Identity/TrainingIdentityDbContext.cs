using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// Represents the database context for identity management, including users and roles.
/// Inherits from <see cref="IdentityDbContext{TUser, TRole, TKey}"/> to provide identity-related functionality.
/// </summary>
public class TrainingIdentityDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrainingIdentityDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the database context.</param>
    public TrainingIdentityDbContext(DbContextOptions<TrainingIdentityDbContext> options)
        : base(options)
    {
    }
}
