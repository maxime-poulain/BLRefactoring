using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the caller-scoped training reads.
/// </summary>
[Collection("Api")]
public sealed class OwnTrainingsTests(ApiFactory factory) : OwnTrainingsTest<ApiFactory>(factory);
