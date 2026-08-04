using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the logging-bridge proof. The test lives in
/// <see cref="LoggingTest{TFactory}"/>; what differs between the two suites is the host whose
/// logger factory answers, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public sealed class LoggingTests(ApiFactory factory) : LoggingTest<ApiFactory>(factory);
