using System.Text;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Pages.Profile;
using TrainingHub.GeneratedClients;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Pages;

/// <summary>
/// Behaviour covered for the profile page.
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

    /// <summary>
    /// Profile tests.
    /// </summary>
    public ProfileTests()
    {
        Services.AddSingleton(_trainerClient.Object);

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
