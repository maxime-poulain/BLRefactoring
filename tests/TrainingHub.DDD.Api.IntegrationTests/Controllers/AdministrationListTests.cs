using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on the administrative listings. The tests live
/// in <see cref="AdministrationListTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministrationListTests(ApiFactory factory) : AdministrationListTest<ApiFactory>(factory);
