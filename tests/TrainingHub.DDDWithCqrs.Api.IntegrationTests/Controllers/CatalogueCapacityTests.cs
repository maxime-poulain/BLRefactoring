using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared catalogue-capacity assertions.
/// </summary>
[Collection("Api")]
public sealed class CatalogueCapacityTests(ApiFactory factory) : CatalogueCapacityTest<ApiFactory>(factory);
