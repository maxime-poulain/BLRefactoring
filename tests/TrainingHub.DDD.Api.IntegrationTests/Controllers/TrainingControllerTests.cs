using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared training-lifecycle facts. The tests live in
/// <see cref="TrainingLifecycleTest{TFactory}"/>. This host gains three assertions in the move: the
/// <c>ETag</c> on a read, and the updated representation and changed version on an edit — all of
/// them things it already did, and none of them things this suite used to check.
/// </summary>
[Collection("Api")]
public sealed class TrainingControllerTests(ApiFactory factory) : TrainingLifecycleTest<ApiFactory>(factory);
