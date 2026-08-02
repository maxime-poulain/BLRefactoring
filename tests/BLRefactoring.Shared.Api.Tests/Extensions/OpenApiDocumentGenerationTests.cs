using AwesomeAssertions;
using BLRefactoring.Shared.Api.Extensions;
using Xunit;

namespace BLRefactoring.Shared.Api.Tests.Extensions;

/// <summary>
/// The guard that keeps client generation away from a database.
/// </summary>
/// <remarks>
/// Emitting the OpenAPI document loads the API host and stops it just before <c>app.Run()</c>, so
/// everything above that line executes — including the call that applies migrations. This decides
/// whether that call is skipped, and getting it wrong in the permissive direction means a
/// generation writes to a real database.
/// </remarks>
public sealed class OpenApiDocumentGenerationTests
{
    /// <summary>
    /// Sets the variable for the duration of a test and puts back whatever was there.
    /// </summary>
    /// <remarks>
    /// Environment variables are process-wide, so a test that leaves one behind changes the
    /// answer for every test that runs after it — in this assembly and, worse, silently.
    /// </remarks>
    private sealed class TemporaryVariable : IDisposable
    {
        private readonly string? _previous =
            Environment.GetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable);

        public TemporaryVariable(string? value) =>
            Environment.SetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, value);

        public void Dispose() =>
            Environment.SetEnvironmentVariable(OpenApiDocumentGeneration.EnvironmentVariable, _previous);
    }

    [Fact]
    public void OrdinaryProcess_IsNotMistakenForGeneration()
    {
        using var _ = new TemporaryVariable(null);

        // The direction that matters: a false positive here would stop a running API from ever
        // migrating its database, silently, and it would start and fail on the first request.
        // This test process is the ordinary case — it is not the document tool.
        OpenApiDocumentGeneration.IsInProgress().Should().BeFalse();
    }

    [Fact]
    public void TheVariableTheScriptSets_TurnsTheGuardOn()
    {
        using var _ = new TemporaryVariable("1");

        OpenApiDocumentGeneration.IsInProgress().Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyValue_DoesNotCount(string value)
    {
        using var _ = new TemporaryVariable(value);

        // An exported-but-empty variable is what a shell leaves behind, and it means nothing was
        // asked for. Treating it as "generating" would disable migrations on a real host.
        OpenApiDocumentGeneration.IsInProgress().Should().BeFalse();
    }
}
