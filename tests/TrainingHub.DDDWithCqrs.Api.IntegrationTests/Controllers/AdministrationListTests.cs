using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on the administrative listings. The tests live in
/// <see cref="AdministrationListTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministrationListTests(ApiFactory factory) : AdministrationListTest<ApiFactory>(factory);
