using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared paging assertions.
/// </summary>
[Collection("Api")]
public sealed class PaginationTests(ApiFactory factory) : PaginationTest<ApiFactory>(factory);
