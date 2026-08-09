using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the public catalogue. The tests live in
/// <see cref="CatalogueSearchTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogueSearchTests(ApiFactory factory) : CatalogueSearchTest<ApiFactory>(factory);
