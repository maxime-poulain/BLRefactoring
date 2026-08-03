using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Fixtures;

/// <summary>
/// The shared fixture for this suite: the layered stack's host, on a real SQL Server.
/// </summary>
/// <remarks>
/// Everything of substance lives in <see cref="ApiFactory{TEntryPoint}"/>, which the CQRS
/// suite shares. Only the entry point differs, and <c>Program</c> here resolves to the one
/// this project references — the layered API.
/// </remarks>
public sealed class ApiFactory : ApiFactory<Program>;

/// <summary>
/// Base class for this suite's tests, fixing the factory type so test classes keep naming
/// <c>ApiFactory</c> instead of carrying a generic argument.
/// </summary>
public abstract class IntegrationTest(ApiFactory factory) : IntegrationTest<ApiFactory>(factory);

/// <summary>
/// Api collection.
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
