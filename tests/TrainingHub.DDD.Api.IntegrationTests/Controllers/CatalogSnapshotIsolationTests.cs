using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertion on snapshot reads. The test lives in
/// <see cref="CatalogSnapshotIsolationTest{TFactory}"/>; running it on both hosts is what makes "the two hosts
/// serve the same API" something other than a sentence in a README.
/// </summary>
[Collection("Api")]
public sealed class CatalogSnapshotIsolationTests(ApiFactory factory)
    : CatalogSnapshotIsolationTest<ApiFactory>(factory);
