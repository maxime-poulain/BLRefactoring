using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the erasure proofs. The tests live in
/// <see cref="AccountErasureTest{TFactory}"/>; the endpoint is shared code in <c>Shared.Api</c>,
/// so what this derivation buys is the whole pipeline around it — the command's staging, the
/// ambient transaction against real SQL Server, the outbox delivering the farewell and
/// collecting the portrait — on this host.
/// </summary>
[Collection("Api")]
public sealed class AccountErasureTests(ApiFactory factory) : AccountErasureTest<ApiFactory>(factory);
