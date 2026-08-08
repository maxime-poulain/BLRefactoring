using Xunit;

namespace TrainingHub.Shared.Api.Tests.Extensions;

/// <summary>
/// Sets an environment variable for the duration of a test and puts back whatever was there.
/// </summary>
/// <remarks>
/// Environment variables are process-wide, so a test that leaves one behind changes the answer for
/// every test that runs after it — in this assembly and, worse, silently.
/// <para>
/// It began as a private class inside <see cref="OpenApiDocumentGenerationTests"/>, which was the
/// only place that touched one. <see cref="AdministratorSeedExtensionsTests"/> needs the same guard
/// for the same variable, and a second copy of nine lines is what ADR 0049's duplication detector
/// exists to see. Extracted rather than repeated.
/// </para>
/// </remarks>
internal sealed class TemporaryEnvironmentVariable : IDisposable
{
    private readonly string _name;

    private readonly string? _previous;

    public TemporaryEnvironmentVariable(string name, string? value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);

        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
}

/// <summary>
/// The collection every class that sets an environment variable belongs to.
/// </summary>
/// <remarks>
/// xUnit runs collections in parallel and gives each class without one a collection of its own. Two
/// classes reading and writing the same variable would then interleave: one sets it, the other
/// restores it, and the first asserts on a value it did not set. The failure is intermittent, which
/// is the worst kind to leave behind for a saving of a few hundred milliseconds.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class EnvironmentVariableCollection
{
    /// <summary>
    /// Name under which the collection is declared and referenced from <c>[Collection(...)]</c>.
    /// </summary>
    public const string Name = "Environment variables";
}
