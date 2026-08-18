using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the verification proofs. The tests live in
/// <see cref="EmailVerificationTest{TFactory}"/>; the flow itself is shared code in
/// <c>Shared.Api</c>, so what this derivation buys is the whole pipeline around it — routing,
/// the rate window, the outbox worker, the mail actually leaving, and this host's own domain
/// check on the transfer (ADR 0090).
/// </summary>
[Collection("Api")]
public sealed class EmailVerificationTests(ApiFactory factory) : EmailVerificationTest<ApiFactory>(factory);
