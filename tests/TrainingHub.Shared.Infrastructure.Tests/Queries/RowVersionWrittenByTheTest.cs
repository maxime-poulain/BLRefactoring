using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TrainingHub.Shared.Infrastructure.Tests.Queries;

/// <summary>
/// Hands the concurrency token back to the application, for the two suites that need it.
/// </summary>
/// <remarks>
/// <c>IsRowVersion</c> names a column SQL Server writes by itself, so EF leaves it out of every
/// insert. SQLite has no such column and no such generator, so the insert would offer nothing
/// for a column declared <c>NOT NULL</c>. Saying the value is never store-generated makes EF
/// send the empty array the aggregate already carries — which is all a read-side suite needs, since
/// it asks a question and never competes for a row.
/// </remarks>
internal sealed class RowVersionWrittenByTheTest(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        ArgumentNullException.ThrowIfNull(modelBuilder);

        var rowVersions = modelBuilder.Model.GetEntityTypes()
            .Select(entity => entity.FindProperty("RowVersion"))
            .OfType<IMutableProperty>();

        foreach (var rowVersion in rowVersions)
        {
            rowVersion.ValueGenerated = ValueGenerated.Never;
        }
    }
}
