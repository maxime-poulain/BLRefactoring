using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared paging assertions.
/// </summary>
[Collection("Api")]
public sealed class PaginationTests(ApiFactory factory) : PaginationTest<ApiFactory>(factory);
