using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The layered host's run of the shared assertions on an account that is nobody's trainer. The
/// tests live in <see cref="AdministratorTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministratorTests(ApiFactory factory) : AdministratorTest<ApiFactory>(factory);
