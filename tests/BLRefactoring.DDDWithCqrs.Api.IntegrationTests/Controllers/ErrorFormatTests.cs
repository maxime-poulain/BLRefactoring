using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the single error shape. The tests live in
/// <see cref="ErrorFormatTest{TFactory}"/>; running them on both hosts is what makes "the two hosts serve
/// the same API" something other than a sentence in a README.
/// </summary>
[Collection("Api")]
public sealed class ErrorFormatTests(ApiFactory factory) : ErrorFormatTest<ApiFactory>(factory);
