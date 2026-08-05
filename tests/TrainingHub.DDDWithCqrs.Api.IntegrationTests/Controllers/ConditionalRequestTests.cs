using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the conditional-request proofs. The tests live in
/// <see cref="ConditionalRequestTest{TFactory}"/>; they started here as host-specific facts,
/// which left the layered host's half of the ETag dance asserted by nothing.
/// </summary>
[Collection("Api")]
public sealed class ConditionalRequestTests(ApiFactory factory) : ConditionalRequestTest<ApiFactory>(factory);
