using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on audit-column precision. The tests live in
/// <see cref="TimestampPrecisionTest{TFactory}"/>; running them on both hosts is what makes "the two hosts serve
/// the same API" something other than a sentence in a README.
/// </summary>
[Collection("Api")]
public sealed class TimestampPrecisionTests(ApiFactory factory) : TimestampPrecisionTest<ApiFactory>(factory);
