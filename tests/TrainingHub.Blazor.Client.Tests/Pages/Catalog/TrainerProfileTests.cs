using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages.Catalog;
using TrainingHub.Blazor.Client.Tests.Infrastructure;
using TrainingHub.GeneratedClients;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalog;

/// <summary>
/// Behavior covered for the trainer's public page.
/// </summary>
/// <remarks>
/// Where "Offered by X" leads (ADR 0070), and the same three answers its sibling distinguishes:
/// here they are, there is nobody to show, and we could not ask. The middle one covers a person
/// who never existed, a suspended one and one with nothing on offer alike, and the page must not
/// report any of them as a failure.
/// </remarks>
public sealed class TrainerProfileTests : ComponentTest
{
    private readonly Mock<ICatalogClient> _catalog = new();

    private readonly Mock<IDialogService> _dialogs = new();

    /// <summary>Trainer profile tests.</summary>
    public TrainerProfileTests()
    {
        Services.AddSingleton(_catalog.Object);
        Services.AddSingleton(_dialogs.Object);
    }

    /// <summary>
    /// Renders, asks the server for the trainer the route names.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheTrainerTheRouteNames()
    {
        var trainerId = Guid.CreateVersion7();

        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offering());

        Render<TrainerProfile>(parameters => parameters
            .Add(page => page.TrainerId, trainerId));

        _catalog.Verify(client => client.GetTrainerProfileAsync(trainerId), Times.Once);
    }

    /// <summary>
    /// Renders, an offering trainer, shows who they are and what they offer.
    /// </summary>
    /// <remarks>
    /// The page in one fact: the name, the bio, and one row per offered training — each leading
    /// back into the catalog, because a shelf whose rows lead nowhere is a dead end (ADR 0070).
    /// </remarks>
    [Fact]
    public void Renders_AnOfferingTrainer_ShowsWhoTheyAreAndWhatTheyOffer()
    {
        var profile = Offering();

        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(profile);

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().Contain("Wrote the first program.");
        page.Markup.Should().Contain("Domain Driven Design");

        page.FindAll("a")
            .Select(anchor => anchor.GetAttribute("href"))
            .Should().Contain($"/catalog/{profile.Trainings[0].Id}");
    }

    /// <summary>
    /// Renders, a publishable portrait, shows it at the address the response named.
    /// </summary>
    /// <remarks>
    /// The profile's own address — the trainer and the photo, what this page has in hand — and no
    /// cache buster, because the identity in the path is what keeps the endpoint's forever-cache
    /// honest (ADR 0063, ADR 0070).
    /// </remarks>
    [Fact]
    public void Renders_APublishablePortrait_ShowsItAtTheAddressTheResponseNamed()
    {
        var photoId = Guid.CreateVersion7();
        var profile = Offering(photoId);

        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(profile);

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain($"api/Catalog/trainers/{profile.Id}/photo/{photoId}");
        page.Markup.Should().NotContain("?v=", "the identity is in the path, so nothing has to bust a cache");
    }

    /// <summary>
    /// Renders, no publishable portrait, shows a name and no image.
    /// </summary>
    /// <remarks>
    /// The response's null covers both "no photo" and "a photo nothing can prove was stripped",
    /// and the page treats them the same rather than rendering an address the endpoint would
    /// answer 404.
    /// </remarks>
    [Fact]
    public void Renders_NoPublishablePortrait_ShowsNoImage()
    {
        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offering());

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().NotContain("/photo/");
    }

    /// <summary>
    /// Renders, a trainer who says nothing about themselves, shows no empty paragraph.
    /// </summary>
    [Fact]
    public void Renders_ATrainerWhoSaysNothing_ShowsNoEmptyParagraph()
    {
        var profile = Offering();
        profile.Bio = null;

        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(profile);

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("Ada Lovelace");
        page.Markup.Should().NotContain("About");
    }

    /// <summary>
    /// Renders, a profile that is not available, says so without reporting a failure.
    /// </summary>
    /// <remarks>
    /// No snackbar, and that is the assertion rather than the panel: an error message would tell a
    /// visitor something went wrong when what happened is that a person has nothing on offer — or
    /// never existed, a difference the page is deliberately not told (ADR 0070).
    /// </remarks>
    [Fact]
    public void Renders_AProfileThatIsNotAvailable_SaysSoWithoutReportingAFailure()
    {
        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.NotFound());

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("This profile is not available");
        Shown().Should().BeEmpty();
    }

    /// <summary>
    /// Renders, the server unreachable, says that rather than that the person is gone.
    /// </summary>
    [Fact]
    public void Renders_TheServerUnreachable_SaysThatRatherThanThatThePersonIsGone()
    {
        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("no route to host"));

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.Markup.Should().Contain("The catalog could not be reached");
        page.Markup.Should().NotContain("This profile is not available");

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The profile could not be loaded.");
    }

    /// <summary>
    /// Renders, an offering trainer, offers to write to them.
    /// </summary>
    /// <remarks>
    /// The button wakes with the interactive runtime rather than with the markup: this page
    /// prerenders (ADR 0072), and a control that looks alive where no dialog can open would
    /// swallow the first click. bUnit runs the interactive lifecycle, so what is pinned here is
    /// the state a visitor ends in — awake — behind a wait that lets the wake-up render happen.
    /// </remarks>
    [Fact]
    public void Renders_AnOfferingTrainer_OffersToWriteToThem()
    {
        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offering());

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.WaitForAssertion(() =>
            Contact(page).HasAttribute("disabled").Should().BeFalse());
    }

    /// <summary>
    /// Contact confirmed, sends what was written to the route's trainer, and names no training.
    /// </summary>
    /// <remarks>
    /// The page's half of the dialog's contract: what came back out of the dialog is what reaches
    /// the API, addressed by the route's identifier — never by anything the response said — and
    /// with no training, because a visitor writing from the person's own page was prompted by
    /// nothing the request could name (ADR 0082).
    /// </remarks>
    [Fact]
    public void ContactConfirmed_SendsWhatWasWritten_AndNamesNoTraining()
    {
        var trainerId = Guid.CreateVersion7();

        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offering());
        WireDialog(DialogResult.Ok(new ContactDraft(
            "Grace", "Hopper", "grace@example.org", "I would like to book a training.",
            Website: null, TurnstileToken: "token-the-widget-minted")));

        // Read at the moment the mocked client is called, because that is the claim: the token
        // must be waiting when the request goes out, for the handler to attach (ADR 0083).
        string? depositedWhenSent = null;
        _catalog
            .Setup(client => client.ContactTrainerAsync(It.IsAny<Guid>(), It.IsAny<ContactTrainerHttpRequest>()))
            .Callback(() => depositedWhenSent =
                Services.GetRequiredService<TurnstileTokenAccessor>().Take())
            .Returns(Task.CompletedTask);

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, trainerId));

        page.WaitForAssertion(() => Contact(page).HasAttribute("disabled").Should().BeFalse());
        Contact(page).Click();

        page.WaitForAssertion(() => _catalog.Verify(
            client => client.ContactTrainerAsync(
                trainerId,
                It.Is<ContactTrainerHttpRequest>(request =>
                    request.SenderFirstname == "Grace"
                    && request.SenderLastname == "Hopper"
                    && request.SenderEmailAddress == "grace@example.org"
                    && request.Message == "I would like to book a training."
                    && request.TrainingId == null
                    && request.Website == null)),
            Times.Once));

        depositedWhenSent.Should().Be("token-the-widget-minted",
            "the draft's token must be deposited before the request, so the handler can attach it");

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
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Offering());
        WireDialog(DialogResult.Cancel());

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

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

    private static AngleSharp.Dom.IElement Contact(IRenderedComponent<TrainerProfile> page) =>
        page.FindAll("button").Single(button => button.TextContent.Contains("Contact", StringComparison.Ordinal));

    private static CatalogTrainerHttpResponse Offering(Guid? photoId = null)
    {
        var trainerId = Guid.CreateVersion7();

        return new CatalogTrainerHttpResponse
        {
            Id = trainerId,
            Firstname = "Ada",
            Lastname = "Lovelace",
            Bio = "Wrote the first program.",
            PhotoId = photoId,
            Trainings =
            [
                new CatalogTrainingHttpResponse
                {
                    Id = Guid.CreateVersion7(),
                    TrainerId = trainerId,
                    Title = "Domain Driven Design"
                }
            ]
        };
    }

    /// <summary>
    /// Renders, the address was refused, shows the document's own words.
    /// </summary>
    [Fact]
    public void Renders_TheAddressWasRefused_ShowsTheDocumentsOwnWords()
    {
        _catalog
            .Setup(client => client.GetTrainerProfileAsync(It.IsAny<Guid>()))
            .ThrowsAsync(ApiExceptions.Refused(400, "The trainerId field must not be the empty identifier."));

        var page = Render<TrainerProfile>(parameters => parameters
            .Add(view => view.TrainerId, Guid.CreateVersion7()));

        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The trainerId field must not be the empty identifier."));
    }
}
