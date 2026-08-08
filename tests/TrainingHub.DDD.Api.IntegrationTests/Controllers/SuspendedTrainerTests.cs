using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on a suspended trainer's surface. The tests live
/// in <see cref="SuspendedTrainerTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class SuspendedTrainerTests(ApiFactory factory) : SuspendedTrainerTest<ApiFactory>(factory);
