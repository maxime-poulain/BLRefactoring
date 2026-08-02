using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;

/// <summary>
/// The shared fixture for this suite: the CQRS stack's host, on a real SQL Server.
/// </summary>
/// <remarks>
/// The implementation is shared with the layered suite — only the entry point differs, and
/// <c>Program</c> here resolves to the CQRS API this project references. Everything the two
/// stacks have in common, down to the EF Core interceptors and the Respawn checkpoint, is
/// therefore exercised identically on both.
/// </remarks>
public sealed class ApiFactory : ApiFactory<Program>;

/// <summary>
/// Base class for this suite's tests, fixing the factory type.
/// </summary>
public abstract class IntegrationTest(ApiFactory factory) : IntegrationTest<ApiFactory>(factory);

/// <summary>
/// Api collection.
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
