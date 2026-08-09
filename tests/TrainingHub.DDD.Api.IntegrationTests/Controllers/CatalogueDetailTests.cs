using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the catalogue's reading of one training. The tests
/// live in <see cref="CatalogueDetailTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogueDetailTests(ApiFactory factory) : CatalogueDetailTest<ApiFactory>(factory);
