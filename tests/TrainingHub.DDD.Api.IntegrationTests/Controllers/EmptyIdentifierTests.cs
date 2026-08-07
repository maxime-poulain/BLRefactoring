using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on the empty identifier. The tests live in
/// <see cref="EmptyIdentifierTest{TFactory}"/>, and this host is the one they were written for:
/// until ADR 0046 it answered 500 where the CQRS host answered 400.
/// </summary>
[Collection("Api")]
public sealed class EmptyIdentifierTests(ApiFactory factory) : EmptyIdentifierTest<ApiFactory>(factory);
