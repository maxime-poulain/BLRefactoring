using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves a photo survives the round trip through a real object store.
/// </summary>
/// <remarks>
/// <para>
/// What an image may be is decided on the value object and tested there, without infrastructure.
/// What is left for this file is everything a unit test has to assume: that bytes written under a
/// key come back byte for byte, that a replacement really does displace the old object, that a
/// deleted photo is gone rather than merely unreferenced, and that the media type survives a trip
/// through S3 and back out of an HTTP response.
/// </para>
/// <para>
/// Run against both suites, like the CORS and ownership assertions beside it. Both hosts publish
/// these three operations because a rule says they must; running the same assertions on both is
/// what makes that parity mean the same behavior rather than the same method names.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class PhotoTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServerErrorSource
{
    /// <summary>
    /// Upload then read, gives back the picture without what the camera wrote into it.
    /// </summary>
    /// <remarks>
    /// This fact used to assert that the bytes come back byte for byte, which ADR 0063 makes wrong:
    /// what is stored has been decoded, oriented, bounded and re-encoded.
    /// <para>
    /// It does not assert byte <em>inequality</em> either, and that is worth writing down because it
    /// was the first correction and it was also wrong. Re-encoding a picture this same library
    /// produced — no metadata, already inside the bound, upright — is deterministic, so the round
    /// trip legitimately gives identical bytes back. CI said so. Byte difference is an accident of
    /// the fixture, not a property of the endpoint.
    /// </para>
    /// <para>
    /// What <em>is</em> a property is that the metadata does not survive, so the upload carries some:
    /// an EXIF description a camera would have written. Whether the sanitizer strips is proved on
    /// real bytes in its own unit tests; what this adds is that the pipeline calls it at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task UploadThenRead_GivesBackThePictureWithoutItsMetadata()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        const string Marker = "TakenAt48.8566,2.3522";

        // Act
        var trainer = await UploadAsync(
            client, Portraits.JpegCarrying(Marker), TrainerPhoto.JpegContentType);

        var response = await client.GetAsync($"/Trainer/{trainer.Id}/photo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(TrainerPhoto.JpegContentType);

        var served = await response.Content.ReadAsByteArrayAsync();

        served.Take(3).Should().Equal([0xFF, 0xD8, 0xFF],
            "the format survives the round trip, whatever happens to the bytes");

        Encoding.ASCII.GetString(served).Should().NotContain(Marker,
            "the description the upload carried is what a phone writes its coordinates beside");
    }

    /// <summary>
    /// Upload, names the photo on the profile so a client can bust its own cache.
    /// </summary>
    [Fact]
    public async Task Upload_NamesThePhotoOnTheProfile()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Act
        var trainer = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        // Assert
        trainer.PhotoId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    /// <summary>
    /// Read, tags the response with the photo's identity.
    /// </summary>
    /// <remarks>
    /// The tag is cut from the photo, not from the row's version. Editing a surname changes the
    /// version and not the portrait, and re-downloading one because of the other is waste.
    /// </remarks>
    [Fact]
    public async Task Read_TagsTheResponseWithThePhotoIdentity()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainer = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        // Act
        var response = await client.GetAsync($"/Trainer/{trainer.Id}/photo");

        // Assert
        response.Headers.ETag!.Tag.Should().Be($"\"{trainer.PhotoId}\"");
    }

    /// <summary>
    /// Read, caller already holding the tag, answers not modified without the bytes.
    /// </summary>
    [Fact]
    public async Task Read_CallerAlreadyHoldingTheTag_AnswersNotModified()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainer = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Trainer/{trainer.Id}/photo");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{trainer.PhotoId}\""));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    /// <summary>
    /// Read, is cached long but revalidated, because this address does not name the photo.
    /// </summary>
    /// <remarks>
    /// The absent <c>immutable</c> is the assertion, not an omission. This address is
    /// <c>/Trainer/{id}/photo</c>, whose bytes change when its owner uploads a new portrait, and
    /// <c>immutable</c> would tell a browser to stop asking for a year. The public portrait's
    /// address carries the photo's identity and says it; this one revalidates (ADR 0063).
    /// </remarks>
    [Fact]
    public async Task Read_IsCachedHard()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainer = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        // Act
        var response = await client.GetAsync($"/Trainer/{trainer.Id}/photo");

        // Assert
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(365));
        response.Headers.CacheControl.Extensions
            .Select(extension => extension.Name)
            .Should().NotContain("immutable");
    }

    /// <summary>
    /// Second upload, replaces the picture and mints a new identity for it.
    /// </summary>
    /// <remarks>
    /// The identity changing is what makes the year-long cache above honest: the new photo is at
    /// a tag no client is holding, so nobody is served the old one out of a cache.
    /// </remarks>
    [Fact]
    public async Task SecondUpload_ReplacesThePictureAndMintsANewIdentity()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var first = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        var replacement = Portraits.Of(TrainerPhoto.JpegContentType);

        // Act
        var second = await UploadAsync(client, replacement, TrainerPhoto.JpegContentType);
        var response = await client.GetAsync($"/Trainer/{second.Id}/photo");

        // Assert
        second.PhotoId.Should().NotBe(first.PhotoId!.Value);
        response.Content.Headers.ContentType!.MediaType.Should().Be(TrainerPhoto.JpegContentType);

        // The picture's format rather than its bytes: what is served has been through the sanitizer,
        // so the question is whether it is the replacement's format and not the first upload's.
        (await response.Content.ReadAsByteArrayAsync()).Take(3).Should().Equal([0xFF, 0xD8, 0xFF]);
    }

    /// <summary>
    /// Delete then read, answers not found.
    /// </summary>
    [Fact]
    public async Task DeleteThenRead_AnswersNotFound()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainer = await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        // Act
        var deleted = await client.DeleteAsync("/Trainer/me/photo");
        var read = await client.GetAsync($"/Trainer/{trainer.Id}/photo");

        // Assert
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Delete twice, answers not found the second time.
    /// </summary>
    [Fact]
    public async Task DeleteTwice_AnswersNotFoundTheSecondTime()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await UploadAsync(client, Portraits.Of(TrainerPhoto.PngContentType), TrainerPhoto.PngContentType);

        // Act
        await client.DeleteAsync("/Trainer/me/photo");
        var second = await client.DeleteAsync("/Trainer/me/photo");

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Read, trainer with no photo, answers not found.
    /// </summary>
    [Fact]
    public async Task Read_TrainerWithNoPhoto_AnswersNotFound()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainer = await client.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");

        // Act
        var response = await client.GetAsync($"/Trainer/{trainer!.Id}/photo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        trainer.PhotoId.Should().BeNull();
    }

    /// <summary>
    /// Upload, bytes disagreeing with the declared media type, is refused in problem+json.
    /// </summary>
    /// <remarks>
    /// The end-to-end half of the rule the value object states: renaming a JPEG does not make it a
    /// PNG, and the refusal comes back in the same shape as every other failure in this API rather
    /// than as whatever the framework would have produced.
    /// </remarks>
    [Fact]
    public async Task Upload_BytesDisagreeingWithTheDeclaredType_IsRefused()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Act
        var response = await PutPhotoAsync(client, Jpeg(), TrainerPhoto.PngContentType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    /// <summary>
    /// Upload, format this API does not publish, is refused.
    /// </summary>
    [Fact]
    public async Task Upload_FormatThisApiDoesNotPublish_IsRefused()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Act
        var response = await PutPhotoAsync(
            client, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(), "image/svg+xml");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Upload, past the aggregate's limit but inside the request's, is refused with its own code.
    /// </summary>
    /// <remarks>
    /// The deterministic half of the size rule. The body is small enough to be accepted by the
    /// server and large enough for the aggregate to refuse it, so the refusal comes from the
    /// domain and says so — a caller reading <c>domainErrors</c> learns which rule it broke rather
    /// than being told only that something was too big.
    /// </remarks>
    [Fact]
    public async Task Upload_PastTheAggregatesLimit_IsRefusedWithItsOwnCode()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var oversized = Png(TrainerPhoto.MaxSizeInBytes + 1);

        // Act
        var response = await PutPhotoAsync(client, oversized, TrainerPhoto.PngContentType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainerErrorCodes.PhotoTooLarge.Value);
    }

    /// <summary>
    /// Upload, past the request's own limit, is refused rather than read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What matters here is that the server stops reading — the alternative is buffering an
    /// arbitrary payload before deciding it was unwelcome, which is a denial of service with extra
    /// steps.
    /// </para>
    /// <para>
    /// The status is asserted as a range rather than a value, and that is a finding rather than a
    /// hedge. A body-read failure inside model binding does not reliably reach the exception
    /// handler that would answer 413: MVC folds it into model state, and the caller is told 400
    /// with an unbound file instead. Both are honest refusals and both stop the read; pinning one
    /// would be pinning a framework's internal routing, not this API's behavior.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Upload_PastTheRequestsLimit_IsRefusedRatherThanRead()
    {
        // Arrange
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var oversized = Png(TrainerPhoto.MaxSizeInBytes + (64 * 1024));

        // Act
        var response = await PutPhotoAsync(client, oversized, TrainerPhoto.PngContentType);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>
    /// Upload, no token, is refused.
    /// </summary>
    [Fact]
    public async Task Upload_NoToken_IsRefused()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await PutPhotoAsync(client, Png(), TrainerPhoto.PngContentType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<TrainerHttpResponse> UploadAsync(
        HttpClient client,
        byte[] content,
        string contentType)
    {
        var response = await PutPhotoAsync(client, content, contentType);

        // Not EnsureSuccessStatusCode. That throws with the status and nothing else, and the status
        // is the least informative thing about a 500 — the body is deliberately generic and the
        // cause is in the host's log, which runs in this very process. Reporting all three turns a
        // red run into a diagnosis instead of the next hypothesis.
        response.IsSuccessStatusCode.Should().BeTrue(
            "the upload should have been accepted, but it answered {0}.\nBody: {1}\nServer logged: {2}",
            (int)response.StatusCode,
            await response.Content.ReadAsStringAsync(),
            LastServerError());

        return (await response.Content.ReadFromJsonAsync<TrainerHttpResponse>())!;
    }

    private string LastServerError()
    {
        var recorded = Factory.ServerErrors;

        if (recorded.Count == 0)
        {
            return "(the host logged nothing at error level)";
        }

        // Truncated, because an annotation nobody can read is worth as much as no annotation. The
        // top of the trace is where the cause is; the rest is the framework getting there.
        var last = recorded[^1];

        return last.Length <= ServerErrorExcerpt ? last : last[..ServerErrorExcerpt] + " […]";
    }

    private const int ServerErrorExcerpt = 1400;

    private static async Task<HttpResponseMessage> PutPhotoAsync(
        HttpClient client,
        byte[] content,
        string contentType)
    {
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "photo", "portrait");

        return await client.PutAsync("/Trainer/me/photo", form);
    }

    /// <summary>Bytes carrying a signature and nothing else: a media type without a picture.</summary>
    private static byte[] Png(int totalSize = 96) =>
        Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], totalSize);

    /// <summary>Bytes carrying a signature and nothing else: a media type without a picture.</summary>
    private static byte[] Jpeg(int totalSize = 96) =>
        Padded([0xFF, 0xD8, 0xFF, 0xE0], totalSize);

    private static byte[] Padded(ReadOnlySpan<byte> signature, int totalSize)
    {
        var content = new byte[totalSize];
        signature.CopyTo(content);

        // Filled with something other than zeroes so "the same bytes came back" is an assertion
        // about the store rather than about two buffers that happen to be empty.
        for (var index = signature.Length; index < content.Length; index++)
        {
            content[index] = (byte)(index % 251);
        }

        return content;
    }
}
