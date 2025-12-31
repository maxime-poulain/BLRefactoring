using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

public class TrainingContext(DbContextOptions<TrainingContext> options )
    : DbContext(options)
{
    public DbSet<Training> Trainings { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingContext).Assembly);
    }
}
