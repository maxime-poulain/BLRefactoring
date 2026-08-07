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

// The shorthand base this file used to carry — `IntegrationTest(ApiFactory)`, fixing the generic
// argument so a test class could name the factory once — went with the last test that declared its
// own facts here. Every class in this suite now derives from a base in the kit, which names the
// factory itself. `EveryAbstractClass_IsActuallyInherited` is what noticed.

/// <summary>
/// Api collection.
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
