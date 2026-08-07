using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared training-lifecycle facts. The tests live in
/// <see cref="TrainingLifecycleTest{TFactory}"/>. This host gains one assertion in the move: a
/// refused delete leaves the row where it was, which only the layered suite used to check.
/// </summary>
[Collection("Api")]
public sealed class TrainingControllerTests(ApiFactory factory) : TrainingLifecycleTest<ApiFactory>(factory);
