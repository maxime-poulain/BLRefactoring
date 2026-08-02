using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

/// <summary>
/// The EF Core session over the trainer and training tables.
/// </summary>
public sealed class TrainingContext(DbContextOptions<TrainingContext> options) : DbContext(options)
{
    /// <summary>
    /// The trainings table.
    /// </summary>
    public DbSet<Training> Trainings { get; set; } = null!;

    /// <summary>
    /// The trainers table.
    /// </summary>
    public DbSet<Trainer> Trainers { get; set; } = null!;

    /// <summary>
    /// Applies every configuration in this assembly.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingContext).Assembly);
    }
}
