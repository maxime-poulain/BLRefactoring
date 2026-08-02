using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BLRefactoring.Shared.Infrastructure.Tests.Interceptors;

/// <summary>
/// The audit stamps, driven by a clock the test controls.
/// </summary>
/// <remarks>
/// This is the coverage injecting <see cref="TimeProvider"/> bought. While the interceptor read
/// <c>DateTime.UtcNow</c>, there was nothing to assert: the value written could only be compared
/// to itself.
/// <para>
/// No real use case creates two aggregates in one <c>SaveChanges</c> — every write path in this
/// solution adds exactly one — so the situation has to be built here on purpose. That is precisely
/// why it deserves a test: nothing else in the suite would notice the day the clock went back to
/// being read once per batch.
/// </para>
/// </remarks>
public sealed class AuditableEntitiesInterceptorTests
{
    /// <summary>A clock that moves on every reading, so equal stamps mean a single reading.</summary>
    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now = _now.AddMilliseconds(1);
    }

    private sealed class AuditedThing : IAuditable
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        // Private setters, as on the real entities: only the interceptor writes them.
        public DateTime CreatedOn { get; private set; }

        public DateTime? ModifiedOn { get; private set; }
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        public DbSet<AuditedThing> Things => Set<AuditedThing>();
    }

    private static TestContext CreateContext(TimeProvider clock) =>
        new(new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntitiesInterceptor(clock))
            .Options);

    /// <remarks>
    /// This asserts what the interceptor writes, which is not on its own what a row ends up
    /// holding: the column's precision decides how much of it survives. It used to be
    /// <c>datetime2(2)</c> — buckets of 10 ms — so stamps a millisecond apart, exactly what this
    /// test produces, were stored as one value and this test proved a property SQL Server undid.
    /// The column is now <c>datetime2(7)</c>, and the distinction reaches the row.
    /// </remarks>
    [Fact]
    public async Task EntitiesSavedTogether_EachCarryTheirOwnInstant()
    {
        await using var context = CreateContext(new SteppingClock(DateTimeOffset.UnixEpoch));

        context.Things.AddRange(new AuditedThing(), new AuditedThing());
        await context.SaveChangesAsync();

        var stamps = await context.Things.Select(thing => thing.CreatedOn).ToListAsync();

        stamps.Should().HaveCount(2);
        stamps.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Creation_StampsCreatedOn_AndLeavesModifiedOnUnset()
    {
        await using var context = CreateContext(new SteppingClock(DateTimeOffset.UnixEpoch));

        context.Things.Add(new AuditedThing());
        await context.SaveChangesAsync();

        var thing = await context.Things.SingleAsync();

        thing.CreatedOn.Should().NotBe(default);
        thing.ModifiedOn.Should().BeNull();
    }

    [Fact]
    public async Task Modification_StampsModifiedOn_AndLeavesCreatedOnAlone()
    {
        await using var context = CreateContext(new SteppingClock(DateTimeOffset.UnixEpoch));

        context.Things.Add(new AuditedThing());
        await context.SaveChangesAsync();

        var thing = await context.Things.SingleAsync();
        var createdOn = thing.CreatedOn;

        context.Entry(thing).State = EntityState.Modified;
        await context.SaveChangesAsync();

        // The creation instant is a fact about the past and must survive every later write.
        thing.CreatedOn.Should().Be(createdOn);
        thing.ModifiedOn.Should().NotBeNull();
    }
}
