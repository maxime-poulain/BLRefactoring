using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the outbox operator surface's proofs. The tests live in
/// <see cref="OutboxOperatorSurfaceTest{TFactory}"/>; what differs between the two suites is the
/// host whose container answers, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class OutboxOperatorSurfaceTests(ApiFactory factory)
    : OutboxOperatorSurfaceTest<ApiFactory>(factory);
