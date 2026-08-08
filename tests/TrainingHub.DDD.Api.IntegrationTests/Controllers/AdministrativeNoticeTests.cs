using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on administrative notices. The tests live in
/// <see cref="AdministrativeNoticeTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministrativeNoticeTests(ApiFactory factory) : AdministrativeNoticeTest<ApiFactory>(factory);
