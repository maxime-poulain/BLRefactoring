using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the empty identifier. The tests live in
/// <see cref="EmptyIdentifierTest{TFactory}"/>. This host already answered 400, from a validation
/// pipeline; it now answers it from the same annotation the layered host does, which is what makes
/// the two answers one answer.
/// </summary>
[Collection("Api")]
public sealed class EmptyIdentifierTests(ApiFactory factory) : EmptyIdentifierTest<ApiFactory>(factory);
