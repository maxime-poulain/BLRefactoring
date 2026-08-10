using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the public catalog. The tests live in
/// <see cref="CatalogSearchTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogSearchTests(ApiFactory factory) : CatalogSearchTest<ApiFactory>(factory);
