using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the outbox proofs. The tests live in
/// <see cref="OutboxTest{TFactory}"/>; what differs between the two suites is the host whose
/// container answers, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class OutboxTests(ApiFactory factory) : OutboxTest<ApiFactory>(factory);
