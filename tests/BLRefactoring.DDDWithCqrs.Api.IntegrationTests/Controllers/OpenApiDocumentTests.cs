using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's copy of the OpenAPI document checks. The tests live in
/// <see cref="OpenApiDocumentTest{TFactory}"/>; this is the host that used to publish no security
/// scheme at all, which is exactly what running them here is for.
/// </summary>
[Collection("Api")]
public sealed class OpenApiDocumentTests(ApiFactory factory) : OpenApiDocumentTest<ApiFactory>(factory);
