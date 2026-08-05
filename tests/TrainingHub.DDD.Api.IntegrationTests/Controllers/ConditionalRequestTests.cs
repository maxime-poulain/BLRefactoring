using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the conditional-request proofs. The tests live in
/// <see cref="ConditionalRequestTest{TFactory}"/>; until they moved there, this host's half of
/// the ETag dance was asserted by nothing.
/// </summary>
[Collection("Api")]
public sealed class ConditionalRequestTests(ApiFactory factory) : ConditionalRequestTest<ApiFactory>(factory);
