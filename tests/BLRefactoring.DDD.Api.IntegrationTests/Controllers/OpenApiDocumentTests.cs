using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's copy of the OpenAPI document checks. The tests live in
/// <see cref="OpenApiDocumentTest{TFactory}"/>; running them on both hosts is what makes the claim
/// that they describe the same API something other than an assertion in a README.
/// </summary>
[Collection("Api")]
public sealed class OpenApiDocumentTests(ApiFactory factory) : OpenApiDocumentTest<ApiFactory>(factory);
