using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Components;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.Blazor.Client.Pages.Profile;
using TrainingHub.GeneratedClients;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behavior covered for the profile page.
/// </summary>
/// <remarks>
/// The first component in this repository with logic worth proving, and the reason bUnit arrived.
/// Three things live only here and nowhere else: the size ceiling restated so a file is refused
/// before it is uploaded, the address that defeats a year-long immutable cache by carrying the
/// photo's identity, and the decision to show the server's refusal in its own words rather than a
/// guess about which rule was broken.
/// </remarks>
public sealed class ProfileTests : ComponentTest
{
    private readonly Mock<ITrainerClient> _trainerClient = new();
    private readonly Mock<IBffSessionClient> _session = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<IAuthenticationStateNotifier> _authentication = new();

    /// <summary>
    /// Profile tests.
    /// </summary>
    public ProfileTests()
    {
        Services.AddSingleton(_trainerClient.Object);
        Services.AddSingleton(_session.Object);
        Services.AddSingleton(_dialogs.Object);
        Services.AddSingleton(_authentication.Object);

        GivenProfile(ProfileWithoutPhoto());
    }

    /// <summary>
    /// Renders, no photo, shows the trainer's initials rather than an image.
    /// </summary>
    [Fact]
    public void Renders_NoPhoto_ShowsTheInitials()
    {
        // Act
        var page = Render<Profile>();

        // Assert
        page.Markup.Should().Contain("JD");
        page.FindAll("img").Should().BeEmpty();
    }

    /// <summary>
    /// Renders, photo published, addresses it with the photo's identity.
    /// </summary>
    /// <remarks>
    /// The identity in the query string is what makes the endpoint's year-long immutable cache
    /// safe to combine with a picture that can change: a replacement mints a new identity, so the
    /// address changes and no browser serves the old portrait from its cache. Losing this line
    /// would show a stale photo for a year and nothing would fail.
    /// </remarks>
    [Fact]
    public void Renders_PhotoPublished_AddressesItWithThePhotoIdentity()
    {
        // Arrange
        var photoId = Guid.NewGuid();
        GivenProfile(ProfileWithPhoto(photoId));

        // Act
        var page = Render<Profile>();

        // Assert
        page.Find("img").GetAttribute("src")
            .Should().Be($"api/Trainer/{TrainerId}/photo?v={photoId}");
    }

    /// <summary>
    /// Renders, photo published, offers to remove it.
    /// </summary>
    [Fact]
    public void Renders_PhotoPublished_OffersToRemoveIt()
    {
        // Arrange
        GivenProfile(ProfileWithPhoto(Guid.NewGuid()));

        // Act
        var page = Render<Profile>();

        // Assert
        page.Markup.Should().Contain("Remove photo").And.Contain("Change photo");
    }

    /// <summary>
    /// Renders, no photo, does not offer to remove one.
    /// </summary>
    [Fact]
    public void Renders_NoPhoto_DoesNotOfferToRemoveOne()
    {
        // Act
        var page = Render<Profile>();

        // Assert
        page.Markup.Should().NotContain("Remove photo").And.Contain("Add a photo");
    }

    /// <summary>
    /// Select photo, past the limit, refuses it without calling the API.
    /// </summary>
    /// <remarks>
    /// The mirror of the server's ceiling, and its only job is speed: telling somebody their forty
    /// megabyte photo is too large should not require uploading forty megabytes first. That the
    /// API is never called is the assertion that matters — the check is worthless if the request
    /// goes out anyway.
    /// </remarks>
    [Fact]
    public async Task SelectPhoto_PastTheLimit_RefusesItWithoutCallingTheApi()
    {
        // Arrange
        var page = Render<Profile>();
        var upload = page.FindComponent<MudFileUpload<IBrowserFile>>();

        // Act
        await page.InvokeAsync(() =>
            upload.Instance.FilesChanged.InvokeAsync(
                new FakeBrowserFile("huge.png", "image/png", 6 * 1024 * 1024)));

        // Assert
        page.Markup.Should().Contain("The limit is 5 MiB");
        _trainerClient.Verify(
            client => client.SetPhotoAsync(It.IsAny<FileParameter>()), Times.Never);
    }

    /// <summary>
    /// Select photo, accepted, uploads it and shows the new address.
    /// </summary>
    [Fact]
    public async Task SelectPhoto_Accepted_UploadsItAndShowsTheNewAddress()
    {
        // Arrange
        var uploaded = Guid.NewGuid();

        _trainerClient
            .Setup(client => client.SetPhotoAsync(It.IsAny<FileParameter>()))
            .ReturnsAsync(ProfileWithPhoto(uploaded));

        var page = Render<Profile>();

        // The upload path reloads the profile to refresh the version it holds, so what the server
        // answers after the write is the profile that now carries the photo. Re-armed after the
        // first render on purpose: armed before it, the address would be on screen from the start
        // and the assertion would prove nothing about the upload.
        GivenProfile(ProfileWithPhoto(uploaded));

        var upload = page.FindComponent<MudFileUpload<IBrowserFile>>();

        // Act
        await page.InvokeAsync(() =>
            upload.Instance.FilesChanged.InvokeAsync(
                new FakeBrowserFile("portrait.png", "image/png", 128)));

        // Assert
        page.Find("img").GetAttribute("src")
            .Should().Be($"api/Trainer/{TrainerId}/photo?v={uploaded}");
    }

    /// <summary>
    /// Select photo, refused by the server, shows the server's own words.
    /// </summary>
    /// <remarks>
    /// The page does not try to work out which rule was broken. The server read the bytes and is
    /// the only thing that can tell an image from a renamed one, so its answer is what a person
    /// sees — reworded refusals are how a client ends up contradicting the API it talks to.
    /// </remarks>
    [Fact]
    public async Task SelectPhoto_RefusedByTheServer_ShowsTheServersOwnWords()
    {
        // Arrange
        _trainerClient
            .Setup(client => client.SetPhotoAsync(It.IsAny<FileParameter>()))
            .ThrowsAsync(new ApiException(
                "Bad Request", 400, "the upload is declared as image/png but its content is image/jpeg",
                new Dictionary<string, IEnumerable<string>>(), null));

        var page = Render<Profile>();
        var upload = page.FindComponent<MudFileUpload<IBrowserFile>>();

        // Act
        await page.InvokeAsync(() =>
            upload.Instance.FilesChanged.InvokeAsync(
                new FakeBrowserFile("portrait.png", "image/png", 128)));

        // Assert
        page.Markup.Should().Contain("its content is image/jpeg");
    }

    /// <summary>
    /// Select photo, nothing selected, does nothing.
    /// </summary>
    [Fact]
    public async Task SelectPhoto_NothingSelected_DoesNothing()
    {
        // Arrange
        var page = Render<Profile>();
        var upload = page.FindComponent<MudFileUpload<IBrowserFile>>();

        // Act
        await page.InvokeAsync(() => upload.Instance.FilesChanged.InvokeAsync(null));

        // Assert
        _trainerClient.Verify(
            client => client.SetPhotoAsync(It.IsAny<FileParameter>()), Times.Never);
    }

    /// <summary>
    /// Remove photo, clears the picture.
    /// </summary>
    [Fact]
    public void RemovePhoto_ClearsThePicture()
    {
        // Arrange
        GivenProfile(ProfileWithPhoto(Guid.NewGuid()));
        var page = Render<Profile>();

        // The removal reloads the profile to refresh the version it holds, so what the server
        // answers after the delete is a profile without a photo.
        GivenProfile(ProfileWithoutPhoto());

        // Act
        page.FindAll("button").Single(button => button.TextContent.Contains("Remove photo", StringComparison.Ordinal)).Click();

        // Assert
        page.WaitForAssertion(() => page.FindAll("img").Should().BeEmpty());
        _trainerClient.Verify(client => client.DeletePhotoAsync(), Times.Once);
    }

    /// <summary>
    /// Remove photo, refused by the server, shows why.
    /// </summary>
    [Fact]
    public void RemovePhoto_RefusedByTheServer_ShowsWhy()
    {
        // Arrange
        GivenProfile(ProfileWithPhoto(Guid.NewGuid()));

        _trainerClient
            .Setup(client => client.DeletePhotoAsync())
            .ThrowsAsync(new ApiException(
                "Not Found", 404, "this trainer has no photo",
                new Dictionary<string, IEnumerable<string>>(), null));

        var page = Render<Profile>();

        // Act
        page.FindAll("button").Single(button => button.TextContent.Contains("Remove photo", StringComparison.Ordinal)).Click();

        // Assert
        page.WaitForAssertion(() => page.Markup.Should().Contain("this trainer has no photo"));
    }

    /// <summary>
    /// Saving after a photo change, sends the version the reload answered.
    /// </summary>
    /// <remarks>
    /// The defect this pins: the photo write bumps the row's version, and the page used to keep
    /// the ETag it read at load. The next save then carried a version naming a row that no longer
    /// existed, and the server refused it with 412 — "someone else changed this profile" — with
    /// nobody else involved. The upload path now reloads, and this proves the save sends what the
    /// reload answered rather than what the first load did.
    /// </remarks>
    [Fact]
    public async Task SavingAfterAPhotoChange_SendsTheVersionTheReloadAnswered()
    {
        // Arrange
        var uploaded = Guid.NewGuid();

        _trainerClient
            .SetupSequence(client => client.GetCurrentAsync())
            .ReturnsAsync(Answering("\"v1\"", ProfileWithoutPhoto()))
            .ReturnsAsync(Answering("\"v2\"", ProfileWithPhoto(uploaded)))
            .ReturnsAsync(Answering("\"v3\"", ProfileWithPhoto(uploaded)));

        _trainerClient
            .Setup(client => client.SetPhotoAsync(It.IsAny<FileParameter>()))
            .ReturnsAsync(ProfileWithPhoto(uploaded));

        _trainerClient
            .Setup(client => client.EditCurrentAsync(It.IsAny<string?>(), It.IsAny<EditTrainerHttpRequest>()))
            .ReturnsAsync(ProfileWithPhoto(uploaded));

        var page = Render<Profile>();
        var upload = page.FindComponent<MudFileUpload<IBrowserFile>>();

        await page.InvokeAsync(() =>
            upload.Instance.FilesChanged.InvokeAsync(
                new FakeBrowserFile("portrait.png", "image/png", 128)));

        // Act
        page.FindAll("button")
            .Single(button => button.TextContent.Contains("Save Changes", StringComparison.Ordinal))
            .Click();

        // Assert
        page.WaitForAssertion(() => _trainerClient.Verify(
            client => client.EditCurrentAsync("\"v2\"", It.IsAny<EditTrainerHttpRequest>()), Times.Once));
    }

    /// <summary>
    /// Renders, profile unreadable, shows a sentence of its own.
    /// </summary>
    /// <remarks>
    /// The message on the exception is the generator's — "The HTTP status code of the response was
    /// not expected (503)." It belongs in the console, not on screen, and this is the test that
    /// keeps it there.
    /// </remarks>
    [Fact]
    public void Renders_ProfileUnreadable_ShowsASentenceOfItsOwn()
    {
        // Arrange
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ThrowsAsync(new ApiException(
                "The HTTP status code of the response was not expected (503).", 503, "",
                new Dictionary<string, IEnumerable<string>>(), null));

        // Act
        Render<Profile>();

        // Assert
        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The profile could not be loaded. Try again in a moment.");
    }

    /// <summary>
    /// Renders, profile unreadable, does not throw.
    /// </summary>
    /// <remarks>
    /// A page that throws while loading takes the whole circuit with it. The failure is reported
    /// and the form stays on screen.
    /// </remarks>
    [Fact]
    public void Renders_ProfileUnreadable_DoesNotThrow()
    {
        // Arrange
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ThrowsAsync(new ApiException(
                "Server Error", 500, "", new Dictionary<string, IEnumerable<string>>(), null));

        // Act
        var act = () => Render<Profile>();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Renders, offers to erase the account.
    /// </summary>
    [Fact]
    public void Renders_OffersToEraseTheAccount()
    {
        // Act
        var page = Render<Profile>();

        // Assert
        page.Markup.Should().Contain("Erase account");
        page.Markup.Should().Contain("for good",
            "the finality is said beside the button, before any dialog opens");
    }

    /// <summary>
    /// Erasing, the password the dialog collected, is what reaches the BFF.
    /// </summary>
    /// <remarks>
    /// The seam that matters is between the dialog's answer and the call: a page that sent an
    /// empty password — or its own invention — would be refused for the wrong reason, or worse,
    /// not refused. What the dialog does with what is typed into it is pinned where it lives, in
    /// <c>EraseAccountDialogTests</c>.
    /// </remarks>
    [Fact]
    public void Erasing_ThePasswordTheDialogCollected_IsWhatReachesTheBff()
    {
        // Arrange
        AnsweringErasure("Password1!");

        _session
            .Setup(client => client.EraseAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProblemDetails?)null);

        var page = Render<Profile>();

        // Act
        EraseButton(page).Click();

        // Assert
        page.WaitForAssertion(() => _session.Verify(
            client => client.EraseAccountAsync("Password1!", It.IsAny<CancellationToken>()), Times.Once));
    }

    /// <summary>
    /// Erasing, the account gone, signs the interface out and leaves.
    /// </summary>
    /// <remarks>
    /// The BFF has already dropped the cookie, so what remains is the interface's half: telling
    /// every <c>AuthorizeView</c> the identity changed, and leaving a page that now describes an
    /// account that does not exist. A page that stayed would show a profile nobody holds.
    /// </remarks>
    [Fact]
    public void Erasing_TheAccountGone_SignsTheInterfaceOutAndLeaves()
    {
        // Arrange
        AnsweringErasure("Password1!");

        _session
            .Setup(client => client.EraseAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProblemDetails?)null);

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var page = Render<Profile>();

        // Act
        EraseButton(page).Click();

        // Assert
        page.WaitForAssertion(() =>
        {
            navigation.Uri.Should().Be("http://localhost/");
            Shown().Should().ContainSingle()
                .Which.Should().Match<Snackbar>(snackbar =>
                    snackbar.Message == "Your account was erased."
                    && snackbar.Severity == Severity.Success);
        });

        _authentication.Verify(notifier => notifier.NotifyAuthenticationChanged(), Times.Once);
    }

    /// <summary>
    /// Erasing, the API refused, says the API's own sentence and stays signed in.
    /// </summary>
    [Fact]
    public void Erasing_TheApiRefused_SaysTheApisOwnSentenceAndStaysSignedIn()
    {
        // Arrange
        AnsweringErasure("wrong-password");

        var problem = new ProblemDetails { Title = "One or more validation errors occurred.", Status = 400 };
        problem.AdditionalProperties["errors"] = JsonSerializer.Deserialize<JsonElement>(
            """{"Password":["The password is incorrect."]}""");

        _session
            .Setup(client => client.EraseAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(problem);

        var page = Render<Profile>();

        // Act
        EraseButton(page).Click();

        // Assert
        page.WaitForAssertion(() => Shown().Should().ContainSingle()
            .Which.Should().Match<Snackbar>(snackbar =>
                snackbar.Message == "The password is incorrect."
                && snackbar.Severity == Severity.Error));

        // A refused erasure erased nothing, so the identity must not be re-read as though it had.
        _authentication.Verify(notifier => notifier.NotifyAuthenticationChanged(), Times.Never);
    }

    /// <summary>
    /// Erasing, backed out of the dialog, sends nothing.
    /// </summary>
    [Fact]
    public void Erasing_BackedOutOfTheDialog_SendsNothing()
    {
        // Arrange
        AnsweringErasure(password: null);

        var page = Render<Profile>();

        // Act
        EraseButton(page).Click();

        // Assert
        page.WaitForAssertion(() => _dialogs.Verify(
            service => service.ShowAsync<EraseAccountDialog>(It.IsAny<string>(), It.IsAny<DialogOptions>()),
            Times.Once));

        _session.Verify(
            client => client.EraseAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Substitutes the dialog's answer, because the page under test renders no dialog provider.
    /// </summary>
    private void AnsweringErasure(string? password)
    {
        var reference = new Mock<IDialogReference>();

        reference
            .Setup(dialog => dialog.Result)
            .ReturnsAsync(password is null ? DialogResult.Cancel() : DialogResult.Ok(password));

        _dialogs
            .Setup(service => service.ShowAsync<EraseAccountDialog>(
                It.IsAny<string>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(reference.Object);
    }

    private static AngleSharp.Dom.IElement EraseButton(IRenderedComponent<Profile> page) =>
        page.FindAll("button").Single(button =>
            button.TextContent.Contains("Erase account", StringComparison.Ordinal));

    private static readonly Guid TrainerId = Guid.NewGuid();

    private void GivenProfile(TrainerHttpResponse trainer) =>
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ReturnsAsync(Answering("\"AAAAAAAAB9E=\"", trainer));

    private static SwaggerResponse<TrainerHttpResponse> Answering(string etag, TrainerHttpResponse trainer) =>
        new(200, new Dictionary<string, IEnumerable<string>> { ["ETag"] = [etag] }, trainer);

    private static TrainerHttpResponse ProfileWithoutPhoto() => new()
    {
        Id = TrainerId,
        Firstname = "John",
        Lastname = "Doe",
        ContactEmail = "john.doe@example.com"
    };

    private static TrainerHttpResponse ProfileWithPhoto(Guid photoId) => new()
    {
        Id = TrainerId,
        Firstname = "John",
        Lastname = "Doe",
        ContactEmail = "john.doe@example.com",
        PhotoId = photoId
    };

    /// <summary>
    /// A file the browser would have handed over, without a browser.
    /// </summary>
    private sealed class FakeBrowserFile(string name, string contentType, long size) : IBrowserFile
    {
        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;

        public long Size { get; } = size;

        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(Encoding.UTF8.GetBytes("not really a portrait"));
    }
}
