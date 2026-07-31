using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Fixtures;

/// <summary>
/// Base class for the integration tests: empties the database before each test.
/// </summary>
/// <remarks>
/// The whole collection shares one SQL Server container, so without this every test
/// inherited the rows its predecessors had left — which is how assertions ended up
/// reading whole tables and picking an arbitrary row from them.
/// Derived classes keep their own <c>[Collection("Api")]</c> attribute rather than
/// inheriting it: xUnit's discovery of inherited collection attributes is not
/// something worth depending on.
/// </remarks>
public abstract class IntegrationTest(ApiFactory factory) : IAsyncLifetime
{
    /// <summary>
    /// The shared web application factory. Exposed here so derived classes use this
    /// one rather than capturing the constructor parameter a second time.
    /// </summary>
    protected ApiFactory Factory { get; } = factory;

    // xUnit builds a new instance of the test class for each test method, so this
    // runs before every single test rather than once per class.
    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
