using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Pages.Catalog;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalog;

/// <summary>
/// Behavior covered for the public catalog's detail page.
/// </summary>
/// <remarks>
/// What a visitor gets when they follow a search result, and the screen that has to distinguish
/// three answers a listing never sees: here it is, there is no such thing on offer, and we could not
/// ask. The middle one is a 404 the page must not report as a failure — a URL somebody kept is
/// allowed to stop working (ADR 0055, ADR 0062).
/// </remarks>
public sealed class CatalogTrainingTests : ComponentTest
{
    private readonly Mock<ICatalogClient> _catalog = new();

    private readonly Mock<IDialogService> _dialogs = new();

    /// <summary>Catalog training tests.</summary>
    public CatalogTrainingTests()
    {
        Services.AddSingleton(_catalog.Object);
        Services.AddSingleton(_dialogs.Object);
    }

    /// <summary>
    /// Renders, asks the server for the training the route names.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheTrainingTheRouteNames()
    {
        var trainingId = Guid.CreateVersion7();

        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        Render<CatalogTraining>(parameters => parameters
            .Add(page => page.TrainingId, trainingId));

        _catalog.Verify(client => client.GetOfferedTrainingAsync(trainingId), Times.Once);
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
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Domain Driven Design");
        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().Contain("Architecture");
        page.Markup.Should().Contain("What a bounded context is.");
    }

    /// <summary>
    /// Renders, an offered training, links its trainer to their page.
    /// </summary>
    /// <remarks>
    /// The navigation ADR 0070 exists for, from the fiche's side: "Offered by X" is an anchor to
    /// the profile built from the identifier the response carries, not a dead label.
    /// </remarks>
    [Fact]
    public void Renders_AnOfferedTraining_LinksItsTrainerToTheirPage()
    {
        var offered = Offered();

        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(offered);

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.FindAll("a")
            .Select(anchor => anchor.GetAttribute("href"))
            .Should().Contain($"/catalog/trainers/{offered.TrainerId}");
    }

    /// <summary>
    /// Renders, a training whose trainer has a publishable portrait, shows it at the address the
    /// response named.
    /// </summary>
    /// <remarks>
    /// The address is what this fact is about rather than the picture: it is built from the
    /// training and the photo — what this page has in hand — which is what keeps the endpoint's
    /// forever-cache honest (ADR 0063).
    /// </remarks>
    [Fact]
    public void Renders_ATrainerWithAPublishablePortrait_ShowsItAtTheAddressTheResponseNamed()
    {
        var photoId = Guid.CreateVersion7();
        var offered = Offered(photoId);

        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(offered);

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain($"api/Catalog/trainings/{offered.Id}/photo/{photoId}");
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
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().NotContain("/photo/");
    }

    /// <summary>
    /// Renders, an offered training, leads each topic back to its shelf.
    /// </summary>
    /// <remarks>
    /// A topic on the sheet is not a label: it is the address of every other training on the same
    /// shelf, so the chip is an anchor into the filtered catalog (ADR 0069).
    /// </remarks>
    [Fact]
    public void Renders_AnOfferedTraining_LeadsEachTopicBackToItsShelf()
    {
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.FindAll("a")
            .Select(anchor => anchor.GetAttribute("href"))
            .Should().Contain("/catalog?topic=Architecture");
    }

    /// <summary>
    /// Renders, an offered training, wears one spine segment per topic in the shelf's hue.
    /// </summary>
    /// <remarks>
    /// The sheet keeps the shelf's language: the left edge carries one band per topic, each in
    /// the same hue that topic's spines and marks wear everywhere else, because the color encodes
    /// which shelves this training sits on rather than decorating the card (ADR 0069).
    /// </remarks>
    [Fact]
    public void Renders_AnOfferedTraining_WearsOneSpineSegmentPerTopic()
    {
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered(topics: ["Design", "Leadership"]));

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        var segments = page.FindAll(".th-sheet-spine > span");

        segments.Should().HaveCount(2);
        segments[0].GetAttribute("style").Should().Contain("--th-spine-design");
        segments[1].GetAttribute("style").Should().Contain("--th-spine-leadership");
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
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.NotFound());

        var page = Render<CatalogTraining>(parameters => parameters
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
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("no route to host"));

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("The catalog could not be reached");
        page.Markup.Should().NotContain("This training is not on offer");

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The training could not be loaded.");
    }

    /// <summary>
    /// Contact confirmed, sends the message to the training's own trainer and names the training.
    /// </summary>
    /// <remarks>
    /// The page's half of the dialog's contract, and the two identifiers each doing their one job:
    /// the trainer the response named is who the message is for, and the training the route names
    /// only says what prompted it — the recipient is decided by the API from the address alone,
    /// so this page never learns where the message ends up (ADR 0082).
    /// </remarks>
    [Fact]
    public void ContactConfirmed_SendsToTheTrainingsTrainer_AndNamesTheTraining()
    {
        var trainingId = Guid.CreateVersion7();
        var training = Offered();

        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(training);
        WireDialog(DialogResult.Ok(new ContactDraft(
            "Grace", "Hopper", "grace@example.org", "I would like to book this training.",
            Website: null, TurnstileToken: "token-the-widget-minted")));

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(view => view.TrainingId, trainingId));

        page.WaitForAssertion(() => Contact(page).HasAttribute("disabled").Should().BeFalse());
        Contact(page).Click();

        page.WaitForAssertion(() => _catalog.Verify(
            client => client.ContactTrainerAsync(
                training.TrainerId,
                It.Is<ContactTrainerHttpRequest>(request =>
                    request.TrainingId == trainingId
                    && request.SenderEmailAddress == "grace@example.org")),
            Times.Once));

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("Your message is on its way to Ada Lovelace.");
    }

    /// <summary>
    /// Contact canceled, sends nothing at all.
    /// </summary>
    [Fact]
    public void ContactCanceled_SendsNothingAtAll()
    {
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offered());
        WireDialog(DialogResult.Cancel());

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(view => view.TrainingId, Guid.CreateVersion7()));

        page.WaitForAssertion(() => Contact(page).HasAttribute("disabled").Should().BeFalse());
        Contact(page).Click();

        page.WaitForAssertion(() => _dialogs.Verify(
            service => service.ShowAsync<ContactTrainerDialog>(
                It.IsAny<string?>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()),
            Times.Once));

        _catalog.Verify(
            client => client.ContactTrainerAsync(It.IsAny<Guid>(), It.IsAny<ContactTrainerHttpRequest>()),
            Times.Never);
    }

    private void WireDialog(DialogResult result)
    {
        var reference = new Mock<IDialogReference>();

        reference
            .SetupGet(dialog => dialog.Result)
            .Returns(Task.FromResult<DialogResult?>(result));

        _dialogs
            .Setup(service => service.ShowAsync<ContactTrainerDialog>(
                It.IsAny<string?>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(reference.Object);
    }

    private static AngleSharp.Dom.IElement Contact(IRenderedComponent<CatalogTraining> page) =>
        page.FindAll("button").Single(button => button.TextContent.Contains("Contact", StringComparison.Ordinal));

    private static CatalogTrainingDetailHttpResponse Offered(
        Guid? trainerPhotoId = null, List<string>? topics = null) => new()
        {
            Id = Guid.CreateVersion7(),
            Title = "Domain Driven Design",
            TrainerName = "Ada Lovelace",
            TrainerId = Guid.CreateVersion7(),
            Topics = topics ?? ["Architecture"],
            Description = "What a bounded context is.",
            Prerequisites = "None.",
            AcquiredSkills = "Drawing a context map.",
            TrainerPhotoId = trainerPhotoId
        };

    /// <summary>
    /// Renders, the address was refused, shows the document's own words.
    /// </summary>
    [Fact]
    public void Renders_TheAddressWasRefused_ShowsTheDocumentsOwnWords()
    {
        _catalog
            .Setup(client => client.GetOfferedTrainingAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.Refused(400, "The trainingId field must not be the empty identifier."));

        var page = Render<CatalogTraining>(parameters => parameters
            .Add(detail => detail.TrainingId, Guid.CreateVersion7()));

        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The trainingId field must not be the empty identifier."));
    }
}
