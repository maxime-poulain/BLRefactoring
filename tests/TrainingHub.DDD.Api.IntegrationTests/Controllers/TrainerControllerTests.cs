using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared trainer-profile facts. The tests live in
/// <see cref="TrainerProfileTest{TFactory}"/>. Until they moved there, this file and its CQRS
/// counterpart asserted the same eight things in two places, and either could have been updated
/// without the other.
/// </summary>
[Collection("Api")]
public sealed class TrainerControllerTests(ApiFactory factory) : TrainerProfileTest<ApiFactory>(factory);
