using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// What the audit columns actually keep, against a real SQL Server.
/// </summary>
/// <remarks>
/// The interceptor reads the clock once per entity so each carries its own instant, and the column
/// decides how much of that survives. It used to be <c>datetime2(2)</c> — two fractional digits,
/// buckets of 10 ms — so anything finer was rounded away on the way in, silently, and no unit test
/// could see it: EF InMemory stores a <c>DateTime</c> as a <c>DateTime</c>.
/// <para>
/// Run against both suites: the interceptor and the column configuration live in the shared
/// infrastructure, so a regression would show on whichever host nobody was testing.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class TimestampPrecisionTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    // 10 ms, the bucket datetime2(2) rounded to.
    private const long TicksPerOldBucket = TimeSpan.TicksPerMillisecond * 10;

    /// <summary>
    /// Stored created on, keeps what the clock gave.
    /// </summary>
    [Fact]
    public async Task StoredCreatedOn_KeepsWhatTheClockGave()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        for (var index = 0; index < 3; index++)
        {
            var response = await client.PostAsJsonAsync(
                "/Training", TrainingRequests.Valid($"Stamped training {index}"));
            response.EnsureSuccessStatusCode();
        }

        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
        var stamps = await context.Trainings.AsNoTracking()
            .Select(training => training.CreatedOn)
            .ToListAsync();

        stamps.Should().HaveCount(3);

        // Asserting the resolution rather than a value or a difference, so the test does not depend
        // on how fast the machine ran: under datetime2(2) every stored instant lands exactly on a
        // 10 ms boundary, always, whatever the timing. Under datetime2(7) a row doing so is a
        // 1-in-100,000 coincidence, and all three doing so is not going to happen.
        stamps.Should().Contain(
            stamp => stamp.Ticks % TicksPerOldBucket != 0,
            "the column must keep the precision the clock provided, not round it to a 10 ms bucket");
    }
}
