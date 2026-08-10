using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the public catalog. The tests live in
/// <see cref="CatalogSearchTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogSearchTests(ApiFactory factory) : CatalogSearchTest<ApiFactory>(factory);
