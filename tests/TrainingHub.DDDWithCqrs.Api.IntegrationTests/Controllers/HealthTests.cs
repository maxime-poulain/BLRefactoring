using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the health endpoints. The tests live in
/// <see cref="HealthTest{TFactory}"/>; running them on both hosts is what makes "every host
/// answers for its own health" (ADR 0037) something other than a sentence in a record.
/// </summary>
[Collection("Api")]
public sealed class HealthTests(ApiFactory factory) : HealthTest<ApiFactory>(factory);
