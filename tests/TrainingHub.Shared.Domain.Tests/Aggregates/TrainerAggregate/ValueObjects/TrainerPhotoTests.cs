using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Behaviour covered for <c>TrainerPhoto</c>.
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
    /// Create, valid PNG, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidPng_ReturnsSuccess()
    {
        // Act
        var result = TrainerPhoto.Create(Png(), TrainerPhoto.PngContentType);

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
        var result = TrainerPhoto.Create(Jpeg(), TrainerPhoto.JpegContentType);

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
        var result = TrainerPhoto.Create(Webp(), TrainerPhoto.WebpContentType);

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
        var photo = TrainerPhoto.Create(content, TrainerPhoto.PngContentType).ShouldBeSuccess();

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
        var first = TrainerPhoto.Create(content, TrainerPhoto.PngContentType).ShouldBeSuccess();
        var second = TrainerPhoto.Create(content, TrainerPhoto.PngContentType).ShouldBeSuccess();

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
        var result = TrainerPhoto.Create([], TrainerPhoto.PngContentType);

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
            Png(TrainerPhoto.MaxSizeInBytes + 1), TrainerPhoto.PngContentType);

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
            Png(TrainerPhoto.MaxSizeInBytes), TrainerPhoto.PngContentType);

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
        var result = TrainerPhoto.Create(Jpeg(), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoContentMismatch);
    }

    /// <summary>
    /// Create, SVG, returns failure.
    /// </summary>
    /// <remarks>
    /// SVG is a document format that executes script. These photos are headed for a public
    /// catalogue, so refusing it is a security decision rather than a formatting preference.
    /// </remarks>
    [Fact]
    public void Create_Svg_ReturnsFailure()
    {
        // Act
        var result = TrainerPhoto.Create(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8,
            "image/svg+xml");

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
        var result = TrainerPhoto.Create("GIF89a......."u8, TrainerPhoto.PngContentType);

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
        var result = TrainerPhoto.Create(Png(), null);

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
        var result = TrainerPhoto.Create(Jpeg(), "image/jpeg; charset=binary");

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
        var result = TrainerPhoto.Create(Png(), "IMAGE/PNG");

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
        var photo = TrainerPhoto.Create(Png(), "IMAGE/PNG").ShouldBeSuccess();

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
        var result = TrainerPhoto.Create("RIFF....WAVEfmt "u8, TrainerPhoto.WebpContentType);

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
        var result = TrainerPhoto.Create("RIFF..."u8, TrainerPhoto.WebpContentType);

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
        var result = TrainerPhoto.Create([0x89, 0x50, 0x4E], TrainerPhoto.PngContentType);

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
        var first = TrainerPhoto.Create(content, TrainerPhoto.PngContentType).ShouldBeSuccess();
        var second = TrainerPhoto.Create(content, TrainerPhoto.PngContentType).ShouldBeSuccess();

        // Assert
        first.Should().NotBe(second);
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
