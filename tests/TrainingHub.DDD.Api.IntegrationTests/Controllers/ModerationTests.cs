using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on the administrative decisions. The tests live
/// in <see cref="ModerationTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class ModerationTests(ApiFactory factory) : ModerationTest<ApiFactory>(factory);
