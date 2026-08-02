using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on model-binding validation. The tests live in
/// <see cref="RequestValidationTest{TFactory}"/>; running them on both hosts is what makes "the two hosts serve
/// the same API" something other than a sentence in a README.
/// </summary>
[Collection("Api")]
public class RequestValidationTests(ApiFactory factory) : RequestValidationTest<ApiFactory>(factory);
