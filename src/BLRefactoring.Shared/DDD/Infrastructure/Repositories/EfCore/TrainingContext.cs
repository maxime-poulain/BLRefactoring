using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;

public class TrainingContext(DbContextOptions<TrainingContext> options, IEventPublisher publisher)
    : DbContext(options)
{
    public DbSet<Training> Trainings { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();

        var entriesWrittenCount = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await publisher.PublishAsync(domainEvents, cancellationToken);
        return entriesWrittenCount;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
