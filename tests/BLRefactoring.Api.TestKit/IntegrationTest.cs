using Microsoft.Extensions.DependencyInjection;
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
    /// <summary>
    /// Returns the database to its empty state between tests.
    /// </summary>
    Task ResetDatabaseAsync();
}

/// <summary>
/// A fixture that lends out the host's own dependency-injection scopes.
/// </summary>
/// <remarks>
/// For the rare test that has to drive the application through its services rather than over
/// HTTP, because the behaviour under test has no endpoint in front of it. Everything resolved
/// from such a scope is the real thing — the request-scoped <c>DbContext</c>, its interceptors,
/// the repositories and the unit of work the host itself would use.
/// </remarks>
public interface IServiceScopeSource
{
    /// <summary>
    /// Opens a scope on the host's container, for reaching a service directly.
    /// </summary>
    IServiceScope CreateScope();
}

/// <summary>
/// A fixture that hands out an <see cref="HttpClient"/> bound to the host under test.
/// </summary>
/// <remarks>
/// Same reason as the two above: a shared test can ask for the capability without naming a
/// concrete factory, which would drag its entry-point type parameter into every signature.
/// <see cref="System.Net.Http.HttpClient"/> is what <c>WebApplicationFactory</c> already exposes,
/// so a fixture satisfies this by declaring it.
/// </remarks>
public interface IHttpClientSource
{
    /// <summary>
    /// Builds a client pointed at the host under test.
    /// </summary>
    HttpClient CreateClient();
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

    /// <summary>
    /// Initialize async.
    /// </summary>
    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    /// <summary>
    /// Dispose async.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
}
