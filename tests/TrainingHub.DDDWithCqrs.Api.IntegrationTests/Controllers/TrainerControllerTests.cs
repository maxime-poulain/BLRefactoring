using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared trainer-profile facts. The tests live in
/// <see cref="TrainerProfileTest{TFactory}"/>. This host reaches them through commands dispatched
/// by Mediator and a representation read back from the query side — a different path to the same
/// answers, which is what the shared base is for.
/// </summary>
[Collection("Api")]
public sealed class TrainerControllerTests(ApiFactory factory) : TrainerProfileTest<ApiFactory>(factory);
