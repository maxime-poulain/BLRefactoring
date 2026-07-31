using Xunit;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// A fixture whose database can be brought back to a known state.
/// </summary>
/// <remarks>
/// Exists so <see cref="IntegrationTest{TFactory}"/> can demand that capability without
/// naming a concrete factory, which would drag its entry-point type parameter along.
/// </remarks>
public interface IResettableDatabase
{
    Task ResetDatabaseAsync();
}

/// <summary>
/// Base class for the integration tests: empties the database before each test.
/// </summary>
/// <remarks>
/// The whole collection shares one SQL Server container, so without this every test
/// inherited the rows its predecessors had left — which is how assertions ended up
/// reading whole tables and picking an arbitrary row from them.
/// Derived classes keep their own <c>[Collection]</c> attribute rather than inheriting
/// it: xUnit's discovery of inherited collection attributes is not something worth
/// depending on.
/// </remarks>
public abstract class IntegrationTest<TFactory>(TFactory factory) : IAsyncLifetime
    where TFactory : IResettableDatabase
{
    /// <summary>
    /// The shared web application factory. Exposed here so derived classes use this
    /// one rather than capturing the constructor parameter a second time.
    /// </summary>
    protected TFactory Factory { get; } = factory;

    // xUnit builds a new instance of the test class for each test method, so this
    // runs before every single test rather than once per class.
    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
