using TrainingHub.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The CQRS host's run of the shared assertions on an account that is nobody's trainer. The tests
/// live in <see cref="AdministratorTest{TFactory}"/>.
/// </summary>
[Collection("Api")]
public sealed class AdministratorTests(ApiFactory factory) : AdministratorTest<ApiFactory>(factory);
