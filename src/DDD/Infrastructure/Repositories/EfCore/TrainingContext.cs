using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore;

public class TrainingContext : DbContext
{
    private readonly IEventPublisher _publisher;

    public TrainingContext(DbContextOptions<TrainingContext> options, IEventPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

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
        await _publisher.PublishAsync(domainEvents, cancellationToken);
        return entriesWrittenCount;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
