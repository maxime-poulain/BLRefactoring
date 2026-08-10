using System.Net;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.Blazor.Client.Pages.Catalogue;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalogue;

/// <summary>
/// Behaviour covered for the public catalogue's detail page.
/// </summary>
/// <remarks>
/// What a visitor gets when they follow a search result, and the screen that has to distinguish
/// three answers a listing never sees: here it is, there is no such thing on offer, and we could not
/// ask. The middle one is a 404 the page must not report as a failure — a URL somebody kept is
/// allowed to stop working (ADR 0055, ADR 0062).
/// </remarks>
public sealed class CatalogueTrainingTests : ComponentTest
{
    private readonly Mock<ICatalogueClient> _catalogue = new();

    /// <summary>Catalogue training tests.</summary>
    public CatalogueTrainingTests() => Services.AddSingleton(_catalogue.Object);

    /// <summary>
    /// Renders, asks the server for the training the route names.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheTrainingTheRouteNames()
    {
        var trainingId = Guid.CreateVersion7();

        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        Render<CatalogueTraining>(parameters => parameters
            .Add(page => page.TrainingId, trainingId));

        _catalogue.Verify(client => client.GetOfferedTrainingAsync(trainingId), Times.Once);
    }

    /// <summary>
    /// Renders, an offered training, names its trainer rather than identifying them.
    /// </summary>
    /// <remarks>
    /// The one column this endpoint reads from the write model rather than the index, and the whole
    /// difference between a page somebody can use and a page showing a GUID.
    /// </remarks>
    [Fact]
    public void Renders_AnOfferedTraining_NamesItsTrainer()
    {
        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        var page = Render<CatalogueTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Domain Driven Design");
        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().Contain("Architecture");
        page.Markup.Should().Contain("What a bounded context is.");
    }

    /// <summary>
    /// Renders, a training whose trainer has a publishable portrait, shows it at the address the
    /// response named.
    /// </summary>
    /// <remarks>
    /// The address is what this fact is about rather than the picture: it is built from the training
    /// and the photo, so a reader can see that no identifier of a person leaves this page (ADR 0063).
    /// </remarks>
    [Fact]
    public void Renders_ATrainerWithAPublishablePortrait_ShowsItAtTheAddressTheResponseNamed()
    {
        var photoId = Guid.CreateVersion7();
        var offered = Offered(photoId);

        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(offered);

        var page = Render<CatalogueTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain($"api/Catalogue/trainings/{offered.Id}/photo/{photoId}");
        page.Markup.Should().NotContain("?v=", "the identity is in the path, so nothing has to bust a cache");
    }

    /// <summary>
    /// Renders, a training whose trainer has no publishable portrait, shows a name and no image.
    /// </summary>
    /// <remarks>
    /// The response's null covers both "no photo" and "a photo nothing can prove was stripped", and
    /// the page treats them the same. What it must not do is render an address the endpoint would
    /// answer 404, which is a broken image rather than no image.
    /// </remarks>
    [Fact]
    public void Renders_ATrainerWithNoPublishablePortrait_ShowsNoImage()
    {
        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        var page = Render<CatalogueTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().NotContain("/photo/");
    }

    /// <summary>
    /// Renders, a training that is not on offer, says so without reporting a failure.
    /// </summary>
    /// <remarks>
    /// No snackbar, and that is the assertion rather than the panel: an error message would tell a
    /// visitor something went wrong when what happened is that a training was withdrawn.
    /// </remarks>
    [Fact]
    public void Renders_ATrainingThatIsNotOnOffer_SaysSoWithoutReportingAFailure()
    {
        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ThrowsAsync(NotFound());

        var page = Render<CatalogueTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("This training is not on offer");
        Shown().Should().BeEmpty();
    }

    /// <summary>
    /// Renders, the server unreachable, says that rather than that the training is gone.
    /// </summary>
    [Fact]
    public void Renders_TheServerUnreachable_SaysThatRatherThanThatTheTrainingIsGone()
    {
        _catalogue
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("no route to host"));

        var page = Render<CatalogueTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("The catalogue could not be reached");
        page.Markup.Should().NotContain("This training is not on offer");

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The training could not be loaded.");
    }

    private static ApiException NotFound() =>
        new(
            "Not Found",
            (int)HttpStatusCode.NotFound,
            response: null,
            headers: new Dictionary<string, IEnumerable<string>>(),
            innerException: null);

    private static CatalogueTrainingDetailHttpResponse Offered(Guid? trainerPhotoId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Title = "Domain Driven Design",
        TrainerName = "Ada Lovelace",
        Topics = ["Architecture"],
        Description = "What a bounded context is.",
        Prerequisites = "None.",
        AcquiredSkills = "Drawing a context map.",
        TrainerPhotoId = trainerPhotoId
    };
}
