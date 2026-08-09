using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the catalogue's reading of one training. The tests
/// live in <see cref="CatalogueDetailTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogueDetailTests(ApiFactory factory) : CatalogueDetailTest<ApiFactory>(factory);
