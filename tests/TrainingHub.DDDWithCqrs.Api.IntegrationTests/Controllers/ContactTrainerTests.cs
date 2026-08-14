using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the contact path proofs. The tests live in
/// <see cref="ContactTrainerTest{TFactory}"/>; what differs between the two suites is the stack
/// carrying the message from the POST to the outbox, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class ContactTrainerTests(ApiFactory factory) : ContactTrainerTest<ApiFactory>(factory);
