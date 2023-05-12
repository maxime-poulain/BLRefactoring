using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore;

public class TrainingContext : DbContext
{
    public TrainingContext(DbContextOptions<TrainingContext> options) : base(options)
    {
    }

    public DbSet<Training> Trainings { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
