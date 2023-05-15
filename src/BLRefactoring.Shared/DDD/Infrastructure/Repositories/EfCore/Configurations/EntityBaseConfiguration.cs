using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore.Configurations;

public abstract class EntityBaseConfiguration<T> : IEntityTypeConfiguration<T>
    where T : AggregateRoot<Guid>
{
    public abstract void ConfigureEntity(EntityTypeBuilder<T> builder);

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd().IsRequired();

        // Special case for aggregate roots as they have a DomainEvents property to be ignored.
        // We could have introduced a `AggregateConfiguration` base class that inherit from this one.
        // But I decided not to, to prevent mistakes or taking the wrong configuration.
        if (typeof(T).IsAssignableTo(typeof(IHasDomainEvents)))
        {
            builder.Ignore(nameof(IHasDomainEvents.DomainEvents));
        }

        builder.Property(e => e.CreatedOn)
            .HasPrecision(2)
            .IsRequired();

        builder.Property(e => e.ModifiedOn)
            .HasPrecision(2);

        builder.Property(e => e.DeletedOn)
            .HasPrecision(2);
        ConfigureEntity(builder);
    }
}
