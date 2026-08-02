using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Configurations;

/// <summary>
/// Shared mapping for every aggregate root: the typed identifier conversion and the row version
/// that carries optimistic concurrency.
/// </summary>
public abstract class AggregateRootTypeConfiguration<TEntity, TEntityId> : IEntityTypeConfiguration<TEntity>
    where TEntity : AggregateRoot<TEntityId>
    where TEntityId : EntityId<TEntityId>
{
    /// <summary>
    /// Maps what is specific to one aggregate, after the shared mapping has been applied.
    /// </summary>
    protected abstract void ConfigureAggregate(EntityTypeBuilder<TEntity> builder);

    /// <summary>
    /// Applies the shared mapping, then hands over to <see cref="ConfigureAggregate"/>.
    /// </summary>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(
                id => id.Value,
                guid => EntityId<TEntityId>.Create(guid))
            .ValueGeneratedOnAdd()
            .IsRequired();

        // Special case for aggregate roots as they have a DomainEvents property to be ignored.
        // We could have introduced a `AggregateConfiguration` base class that inherit from this one.
        // But I decided not to, to prevent mistakes or taking the wrong configuration.
        if (typeof(TEntity).IsAssignableTo(typeof(IHasDomainEvents)))
        {
            builder.Ignore(nameof(IHasDomainEvents.DomainEvents));
        }

        // Full precision, so a stamp read back is the stamp that was written: a DateTime carries
        // 100-nanosecond ticks and seven fractional digits store all of them. It used to be two —
        // datetime2(2), buckets of 10 ms — which silently undid the interceptor reading the clock
        // once per entity, since two saves 3 ms apart landed on the same stored value.
        // Declared rather than inherited: datetime2(7) is already EF's default here, and an
        // implicit default is exactly what let the 2 go unnoticed.
        builder.Property(e => e.CreatedOn)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(e => e.ModifiedOn)
            .HasPrecision(7);

        // Optimistic concurrency, declared once for every aggregate root: the
        // aggregate is the consistency boundary, so it is the unit of concurrency
        // control. IsRowVersion maps to SQL Server's rowversion column, which the
        // server bumps on every update, and makes it the concurrency token — EF
        // adds it to the WHERE clause of UPDATE and DELETE, so a statement that
        // matches no row means someone else got there first.
        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        ConfigureAggregate(builder);
    }
}
