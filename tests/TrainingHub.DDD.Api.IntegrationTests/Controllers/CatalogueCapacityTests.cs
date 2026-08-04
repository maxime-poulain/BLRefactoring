using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared catalogue-capacity assertions.
/// </summary>
[Collection("Api")]
public sealed class CatalogueCapacityTests(ApiFactory factory) : CatalogueCapacityTest<ApiFactory>(factory);
