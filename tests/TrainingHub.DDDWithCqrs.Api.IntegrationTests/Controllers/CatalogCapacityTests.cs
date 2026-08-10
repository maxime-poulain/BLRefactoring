using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared catalog-capacity assertions.
/// </summary>
[Collection("Api")]
public sealed class CatalogCapacityTests(ApiFactory factory) : CatalogCapacityTest<ApiFactory>(factory);
