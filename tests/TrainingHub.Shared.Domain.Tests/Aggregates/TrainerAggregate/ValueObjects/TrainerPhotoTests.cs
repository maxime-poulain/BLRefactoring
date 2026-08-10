using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>TrainerPhoto</c>.
/// </summary>
/// <remarks>
/// This is where the upload rules are actually proven. The endpoint that accepts a photo has an
/// object store behind it and needs Docker to exercise; the rule that decides whether bytes are a
/// photo at all needs nothing, so it is tested here and the integration suite is left to prove the
/// round trip rather than the arithmetic.
/// </remarks>
public sealed class TrainerPhotoTests
{
    /// <summary>
    /// When the bytes handed to the factory were stripped.
    /// </summary>
    /// <remarks>
    /// A fixed instant rather than "now": every fact below is about the bytes, and a clock reading
    /// that changed per run would be one more thing a reader has to rule out when one of them
    /// fails. The factory's own contract — that it always stamps — is asserted on its own.
    /// </remarks>
    private static readonly DateTime SanitizedAt = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Create, an accepted photo, records when it was stripped and says it may be published.
    /// </summary>
    /// <remarks>
    /// The invariant ADR 0063 rests on: this factory has no way to mint an unstripped photo, so a
    /// missing stamp can only have come out of a row written before that record. The public
    /// portrait refuses to serve those, and the absence is therefore a fact about history rather
    /// than a state this code can reach.
    /// <para>
    /// The other half — that a photo with no stamp says so — cannot be built here, because nothing
    /// but the database can produce one. It is asserted where such a row actually exists, against
    /// SQLite, in <c>CatalogDetailQueryTests</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void Create_AnAcceptedPhoto_RecordsWhenItWasStripped()
    {
        var photo = TrainerPhoto
            .Create(Png(), TrainerPhoto.PngContentType, SanitizedAt)
            .ShouldBeSuccess();

        photo.SanitizedOnUtc.Should().Be(SanitizedAt);
        photo.MayBePublished.Should().BeTrue();
    }

    /// <summary>
    /// Create, valid PNG, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidPng_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Png(), TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid JPEG, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidJpeg_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Jpeg(), TrainerPhoto.JpegContentType, SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid WebP, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidWebp_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Webp(), TrainerPhoto.WebpContentType, SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid PNG, describes the bytes it was given.
    /// </summary>
    [Fact]
    public void Create_ValidPng_DescribesTheBytes()
    {
        // Arrange
        var content = Png(128);

        // Act
        var photo = TrainerPhoto.Create(content, TrainerPhoto.PngContentType, SanitizedAt).ShouldBeSuccess();

        // Assert
        photo.ContentType.Should().Be(TrainerPhoto.PngContentType);
        photo.ByteSize.Should().Be(128);
        photo.PhotoId.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Create, called twice with the same bytes, mints a different identity each time.
    /// </summary>
    /// <remarks>
    /// This is the property replacement safety rests on. A new identity means a new storage key,
    /// which means the new bytes are written somewhere the old ones are not — so the profile row
    /// can be committed before anything is deleted, and never names an object that is missing.
    /// Reusing an identity would turn every replacement into an overwrite in place.
    /// </remarks>
    [Fact]
    public void Create_CalledTwice_MintsAFreshIdentity()
    {
        // Arrange
        var content = Png();

        // Act
        var first = TrainerPhoto.Create(content, TrainerPhoto.PngContentType, SanitizedAt).ShouldBeSuccess();
        var second = TrainerPhoto.Create(content, TrainerPhoto.PngContentType, SanitizedAt).ShouldBeSuccess();

        // Assert
        second.PhotoId.Should().NotBe(first.PhotoId);
    }

    /// <summary>
    /// Create, no bytes, returns failure.
    /// </summary>
    [Fact]
    public void Create_NoBytes_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create([], TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoEmpty);
    }

    /// <summary>
    /// Create, one byte past the limit, returns failure.
    /// </summary>
    [Fact]
    public void Create_OneBytePastTheLimit_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create(
            Png(TrainerPhoto.MaxSizeInBytes + 1), TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoTooLarge);
    }

    /// <summary>
    /// Create, exactly at the limit, returns success.
    /// </summary>
    /// <remarks>
    /// The pair with the test above is the point: a limit that rejects the value it names is a
    /// different limit, and off-by-one is the failure this size check is most likely to have.
    /// </remarks>
    [Fact]
    public void Create_ExactlyAtTheLimit_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(
            Png(TrainerPhoto.MaxSizeInBytes), TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, JPEG bytes declared as PNG, returns failure.
    /// </summary>
    /// <remarks>
    /// The headline case: renaming a file changes its extension and its <c>Content-Type</c>, and
    /// neither is evidence of anything. The signature is.
    /// </remarks>
    [Fact]
    public void Create_JpegDeclaredAsPng_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create(Jpeg(), TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoContentMismatch);
    }

    /// <summary>
    /// Create, SVG, returns failure.
    /// </summary>
    /// <remarks>
    /// SVG is a document format that executes script. These photos are headed for a public
    /// catalog, so refusing it is a security decision rather than a formatting preference.
    /// </remarks>
    [Fact]
    public void Create_Svg_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8,
            "image/svg+xml", SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Create, GIF declared as PNG, returns failure.
    /// </summary>
    [Fact]
    public void Create_GifDeclaredAsPng_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create("GIF89a......."u8, TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Create, no declared media type, returns failure.
    /// </summary>
    [Fact]
    public void Create_NoDeclaredMediaType_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create(Png(), null, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Create, media type carrying a parameter, returns success.
    /// </summary>
    /// <remarks>
    /// A browser may legitimately send <c>image/jpeg; charset=binary</c>. Rejecting that would be
    /// a refusal the person uploading could do nothing about.
    /// </remarks>
    [Fact]
    public void Create_MediaTypeCarryingAParameter_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Jpeg(), "image/jpeg; charset=binary", SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, media type in a different case, returns success.
    /// </summary>
    [Fact]
    public void Create_MediaTypeInADifferentCase_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Png(), "IMAGE/PNG", SanitizedAt);

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, media type in a different case, still records the canonical one.
    /// </summary>
    /// <remarks>
    /// What gets stored is what the bytes say, spelled one way. Otherwise the media type served
    /// back later would echo however the uploader happened to type it.
    /// </remarks>
    [Fact]
    public void Create_MediaTypeInADifferentCase_RecordsTheCanonicalOne()
    {
        // Act
        var photo = TrainerPhoto.Create(Png(), "IMAGE/PNG", SanitizedAt).ShouldBeSuccess();

        // Assert
        photo.ContentType.Should().Be(TrainerPhoto.PngContentType);
    }

    /// <summary>
    /// Create, RIFF container that is not WebP, returns failure.
    /// </summary>
    /// <remarks>
    /// A WAV file opens with the same four bytes as a WebP. Reading only those four would accept
    /// audio as a portrait, which is why the check reads the marker at offset eight as well.
    /// </remarks>
    [Fact]
    public void Create_RiffContainerThatIsNotWebp_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create("RIFF....WAVEfmt "u8, TrainerPhoto.WebpContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Create, RIFF header cut short, returns failure rather than reading past the end.
    /// </summary>
    [Fact]
    public void Create_RiffHeaderCutShort_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create("RIFF..."u8, TrainerPhoto.WebpContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Create, PNG signature cut short, returns failure.
    /// </summary>
    [Fact]
    public void Create_PngSignatureCutShort_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create([0x89, 0x50, 0x4E], TrainerPhoto.PngContentType, SanitizedAt);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    /// <summary>
    /// Two photos built from the same bytes are not equal, because their identities differ.
    /// </summary>
    [Fact]
    public void Equality_TwoPhotosFromTheSameBytes_AreNotEqual()
    {
        // Arrange
        var content = Png();

        // Act
        var first = TrainerPhoto.Create(content, TrainerPhoto.PngContentType, SanitizedAt).ShouldBeSuccess();
        var second = TrainerPhoto.Create(content, TrainerPhoto.PngContentType, SanitizedAt).ShouldBeSuccess();

        // Assert
        first.Should().NotBe(second);
    }

    /// <summary>
    /// Vet, a photograph past the limit, refuses it before anything can shrink it.
    /// </summary>
    /// <remarks>
    /// One of the two rules that exist only because this method does. Sanitization bounds the
    /// longest side, so a twelve-megapixel upload comes back well under
    /// <see cref="TrainerPhoto.MaxSizeInBytes"/> — and a limit asked after that would never fire
    /// again. It is a rule about what a caller may send, so it is asked of what they sent
    /// (ADR 0063).
    /// </remarks>
    [Fact]
    public void Vet_OneBytePastTheLimit_RefusesTheUpload()
    {
        // Act
        var result = TrainerPhoto.Vet(Png(TrainerPhoto.MaxSizeInBytes + 1), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoTooLarge);
    }

    /// <summary>
    /// Vet, exactly at the limit, accepts it.
    /// </summary>
    [Fact]
    public void Vet_ExactlyAtTheLimit_AcceptsTheUpload()
    {
        // Act
        var result = TrainerPhoto.Vet(Png(TrainerPhoto.MaxSizeInBytes), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldBeSuccess().Should().Be(TrainerPhoto.PngContentType);
    }

    /// <summary>
    /// Vet, JPEG bytes declared as PNG, refuses the upload.
    /// </summary>
    /// <remarks>
    /// The other rule that exists only because this method does, and the sharper of the two. A
    /// sanitizer re-encodes into the format it is told, so a JPEG uploaded as <c>image/png</c>
    /// comes back a real PNG — and a mismatch asked after that has nothing left to find. The lie is
    /// in the upload, so the question belongs to the upload.
    /// </remarks>
    [Fact]
    public void Vet_JpegDeclaredAsPng_RefusesTheUpload()
    {
        // Act
        var result = TrainerPhoto.Vet(Jpeg(), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoContentMismatch);
    }

    /// <summary>
    /// Vet, an accepted upload, answers the media type its bytes really are.
    /// </summary>
    /// <remarks>
    /// What it answers is what the sanitizer is told to write back, so the format survives the
    /// round trip rather than being normalized — a trainer who uploads a PNG with a transparent
    /// background is not handed back a JPEG on a white square.
    /// </remarks>
    [Fact]
    public void Vet_AnAcceptedUpload_AnswersTheMediaTypeItsBytesReallyAre()
    {
        // Act
        var result = TrainerPhoto.Vet(Webp(), "IMAGE/WEBP; charset=binary");

        // Assert
        result.ShouldBeSuccess().Should().Be(TrainerPhoto.WebpContentType,
            "the canonical spelling is the one the aggregate publishes, whatever the caller wrote");
    }

    /// <summary>
    /// Vet, no bytes at all, refuses the upload.
    /// </summary>
    [Fact]
    public void Vet_NoBytes_RefusesTheUpload()
    {
        // Act
        var result = TrainerPhoto.Vet([], TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoEmpty);
    }

    /// <summary>
    /// Vet, a format this API does not publish, refuses the upload.
    /// </summary>
    /// <remarks>
    /// SVG is the case worth naming: it is an image by every ordinary reckoning and it carries
    /// script, which matters more now that these bytes are headed for a page anybody may open.
    /// </remarks>
    [Fact]
    public void Vet_Svg_RefusesTheUpload()
    {
        // Act
        var result = TrainerPhoto.Vet("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8, "image/svg+xml");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
    }

    private static byte[] Png(int totalSize = 64) =>
        WithSignature([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], totalSize);

    private static byte[] Jpeg(int totalSize = 64) =>
        WithSignature([0xFF, 0xD8, 0xFF, 0xE0], totalSize);

    private static byte[] Webp(int totalSize = 64)
    {
        var content = WithSignature("RIFF"u8.ToArray(), totalSize);
        "WEBP"u8.CopyTo(content.AsSpan(8));

        return content;
    }

    private static byte[] WithSignature(byte[] signature, int totalSize)
    {
        var content = new byte[Math.Max(signature.Length, totalSize)];
        signature.CopyTo(content, 0);

        return content;
    }
}
