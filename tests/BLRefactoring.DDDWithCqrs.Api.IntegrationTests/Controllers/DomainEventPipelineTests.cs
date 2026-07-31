using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the domain-event pipeline proof. The test itself lives in
/// <see cref="DomainEventPipelineTest{TFactory}"/>; what differs between the two suites is the
/// host whose container answers, which is exactly what is being checked.
/// </summary>
[Collection("Api")]
public class DomainEventPipelineTests(ApiFactory factory) : DomainEventPipelineTest<ApiFactory>(factory);
