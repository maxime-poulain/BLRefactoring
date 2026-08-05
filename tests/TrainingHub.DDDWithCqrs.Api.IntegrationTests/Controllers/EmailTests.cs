using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the email delivery proofs. The tests live in
/// <see cref="EmailTest{TFactory}"/>; what differs between the two suites is the host whose
/// outbox worker delivers, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class EmailTests(ApiFactory factory) : EmailTest<ApiFactory>(factory);
