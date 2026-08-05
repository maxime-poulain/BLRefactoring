using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared training-transfer assertions.
/// </summary>
[Collection("Api")]
public sealed class TrainingTransferTests(ApiFactory factory) : TrainingTransferTest<ApiFactory>(factory);
