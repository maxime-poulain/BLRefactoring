using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on administrative notices. The tests live in
/// <see cref="AdministrativeNoticeTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministrativeNoticeTests(ApiFactory factory) : AdministrativeNoticeTest<ApiFactory>(factory);
