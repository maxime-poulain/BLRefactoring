using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the recovery proofs. The tests live in
/// <see cref="PasswordRecoveryTest{TFactory}"/>; the flow itself is shared code in
/// <c>Shared.Api</c>, so what this derivation buys is the whole pipeline around it — routing,
/// binding, the outbox worker and the mail actually leaving — on this host.
/// </summary>
[Collection("Api")]
public sealed class PasswordRecoveryTests(ApiFactory factory) : PasswordRecoveryTest<ApiFactory>(factory);
