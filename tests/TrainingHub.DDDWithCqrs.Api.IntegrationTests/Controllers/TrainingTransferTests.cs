using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared training-transfer assertions.
/// </summary>
[Collection("Api")]
public sealed class TrainingTransferTests(ApiFactory factory) : TrainingTransferTest<ApiFactory>(factory);
