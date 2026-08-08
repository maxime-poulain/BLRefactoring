using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the administrative decisions. The tests live in
/// <see cref="ModerationTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class ModerationTests(ApiFactory factory) : ModerationTest<ApiFactory>(factory);
