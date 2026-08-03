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

        GivenProfile(new TrainerResponseHttp
        {
            Id = TrainerId,
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com"
        });
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

    private void GivenProfile(TrainerResponseHttp trainer) =>
        _trainerClient
            .Setup(client => client.GetCurrentAsync())
            .ReturnsAsync(new SwaggerResponse<TrainerResponseHttp>(
                200,
                new Dictionary<string, IEnumerable<string>> { ["ETag"] = ["\"AAAAAAAAB9E=\""] },
                trainer));

    private static TrainerResponseHttp ProfileWithPhoto(Guid photoId) => new()
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
