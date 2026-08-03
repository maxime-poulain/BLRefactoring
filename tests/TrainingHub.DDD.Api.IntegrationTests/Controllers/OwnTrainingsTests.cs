using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on the caller-scoped training reads.
/// </summary>
[Collection("Api")]
public sealed class OwnTrainingsTests(ApiFactory factory) : OwnTrainingsTest<ApiFactory>(factory);
