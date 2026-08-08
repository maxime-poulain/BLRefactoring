using AwesomeAssertions;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Extensions;

/// <summary>
/// The guard that keeps client generation away from a database.
/// </summary>
/// <remarks>
/// Emitting the OpenAPI document loads the API host and stops it just before <c>app.Run()</c>, so
/// everything above that line executes — including the call that applies migrations. This decides
/// whether that call is skipped, and getting it wrong in the permissive direction means a
/// generation writes to a real database.
/// </remarks>
[Collection(EnvironmentVariableCollection.Name)]
public sealed class OpenApiDocumentGenerationTests
{
    /// <summary>
    /// Ordinary process, is not mistaken for generation.
    /// </summary>
    [Fact]
    public void OrdinaryProcess_IsNotMistakenForGeneration()
    {
        using var _ = new TemporaryEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, null);

        // The direction that matters: a false positive here would stop a running API from ever
        // migrating its database, silently, and it would start and fail on the first request.
        // This test process is the ordinary case — it is not the document tool.
        OpenApiDocumentGeneration.IsInProgress().Should().BeFalse();
    }

    /// <summary>
    /// The variable the script sets, turns the guard on.
    /// </summary>
    [Fact]
    public void TheVariableTheScriptSets_TurnsTheGuardOn()
    {
        using var _ = new TemporaryEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, "1");

        OpenApiDocumentGeneration.IsInProgress().Should().BeTrue();
    }

    /// <summary>
    /// An empty value, does not count.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyValue_DoesNotCount(string value)
    {
        using var _ = new TemporaryEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, value);

        // An exported-but-empty variable is what a shell leaves behind, and it means nothing was
        // asked for. Treating it as "generating" would disable migrations on a real host.
        OpenApiDocumentGeneration.IsInProgress().Should().BeFalse();
    }
}
