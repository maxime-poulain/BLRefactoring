using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// This host's run of the shared assertions on the catalog's reading of one offering trainer. The
/// tests live in <see cref="CatalogTrainerProfileTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class CatalogTrainerProfileTests(ApiFactory factory)
    : CatalogTrainerProfileTest<ApiFactory>(factory);
