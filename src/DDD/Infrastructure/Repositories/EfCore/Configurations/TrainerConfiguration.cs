using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.DDD.Infrastructure.Repositories.EfCore.Configurations;

public class TrainerConfiguration : EntityBaseConfiguration<Trainer>
{
    public override void ConfigureEntity(EntityTypeBuilder<Trainer> builder)
    {
    }
}
