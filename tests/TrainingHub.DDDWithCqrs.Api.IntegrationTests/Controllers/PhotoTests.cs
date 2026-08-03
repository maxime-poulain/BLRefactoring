using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on trainer photos. The tests live in
/// <see cref="PhotoTest{TFactory}"/>; running them on both hosts is what turns "both publish the
/// same operations" from a claim about method names into one about behaviour.
/// </summary>
[Collection("Api")]
public sealed class PhotoTests(ApiFactory factory) : PhotoTest<ApiFactory>(factory);
