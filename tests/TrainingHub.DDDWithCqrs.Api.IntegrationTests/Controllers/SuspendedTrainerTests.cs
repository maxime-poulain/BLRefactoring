using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on a suspended trainer's surface. The tests live
/// in <see cref="SuspendedTrainerTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class SuspendedTrainerTests(ApiFactory factory) : SuspendedTrainerTest<ApiFactory>(factory);
