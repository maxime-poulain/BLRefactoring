using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Queries;

/// <summary>
/// The ownership question, asked before the action runs.
/// </summary>
/// <remarks>
/// <c>TrainingOwnerAuthorizationHandler</c> calls this from an authorization policy, which ASP.NET
/// Core runs <em>before</em> the action and therefore before any validation pipeline. That order is
/// what makes this method's treatment of an empty identifier a question about status codes: it used
/// to hand <see cref="Guid.Empty"/> straight to <c>TrainingId.Create</c>, which refuses it, so every
/// guarded command route answered 500 for a request the pipeline was standing by to refuse with a
/// 400. Nothing noticed, because nothing had ever sent an empty identifier at a guarded route.
/// </remarks>
public sealed class TrainingOwnerQueryTests
{
    private static TrainingContext CreateUnconnectedContext() =>
        new(new DbContextOptionsBuilder<TrainingContext>()
            // A syntactically valid connection string nothing will ever open — which is the whole
            // assertion below: the answer has to come back without asking the database.
            .UseSqlServer("Server=unreachable;Database=unused;Integrated Security=false;User Id=nobody;Password=nothing;TrustServerCertificate=true")
            .Options);

    /// <summary>
    /// Get owner id, empty identifier, answers no owner without asking the database.
    /// </summary>
    /// <remarks>
    /// Two failures in one assertion, and the context is what separates them. If the guard is gone,
    /// this throws <see cref="ArgumentException"/> out of <c>TrainingId.Create</c> — the 500 this
    /// exists to prevent. If the guard were written after the query instead of before it, the call
    /// would try to open an unreachable connection and fail differently. Only answering
    /// <see langword="null"/> immediately passes.
    /// </remarks>
    [Fact]
    public async Task GetOwnerId_EmptyIdentifier_AnswersNoOwnerWithoutAskingTheDatabase()
    {
        await using var context = CreateUnconnectedContext();
        var sut = new TrainingOwnerQuery(context);

        var ownerId = await sut.GetOwnerIdAsync(Guid.Empty);

        // Null is what "no such training" already meant here, and an empty identifier names none.
        // The authorization handler reads it that way and lets the request through, so the action —
        // or the pipeline in front of it — answers, which is where the decision belongs.
        ownerId.Should().BeNull();
    }
}
