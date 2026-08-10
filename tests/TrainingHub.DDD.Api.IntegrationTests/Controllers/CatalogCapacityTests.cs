using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared catalog-capacity assertions.
/// </summary>
[Collection("Api")]
public sealed class CatalogCapacityTests(ApiFactory factory) : CatalogCapacityTest<ApiFactory>(factory);
