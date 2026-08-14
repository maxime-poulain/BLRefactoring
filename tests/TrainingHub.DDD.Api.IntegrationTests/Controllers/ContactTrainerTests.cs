using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the contact path proofs. The tests live in
/// <see cref="ContactTrainerTest{TFactory}"/>; what differs between the two suites is the stack
/// carrying the message from the POST to the outbox, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class ContactTrainerTests(ApiFactory factory) : ContactTrainerTest<ApiFactory>(factory);
