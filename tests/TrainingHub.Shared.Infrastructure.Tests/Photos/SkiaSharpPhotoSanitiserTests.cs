using System.Text;
using AwesomeAssertions;
using SkiaSharp;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Photos;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Photos;

/// <summary>
/// What happens to the bytes a trainer uploads, before anything stores them (ADR 0063).
/// </summary>
/// <remarks>
/// Against real images and a real decoder, because every claim here is a claim about pixels: that
/// what a camera wrote is gone, that the picture is still the right way up once it is, and that a
/// file which lies about being an image is refused rather than half-processed. A substitute for the
/// decoder would prove that a method was called.
/// <para>
/// The EXIF is built by hand rather than committed as a binary fixture. A checked-in photograph
/// would be opaque — a reader could not tell what it was supposed to contain — and this way the
/// test states the metadata it is about to destroy.
/// </para>
/// </remarks>
public sealed class SkiaSharpPhotoSanitiserTests
{
    /// <summary>What a phone would have written into the file, in a form a test can look for.</summary>
    private const string WhereItWasTaken = "48.8566,2.3522";

    private readonly SkiaSharpPhotoSanitiser _sanitiser = new();

    /// <summary>
    /// Sanitise, a photo carrying what a camera wrote, keeps none of it.
    /// </summary>
    /// <remarks>
    /// The fact ADR 0021 named and deferred, and the reason the portrait could not be published:
    /// <em>"a photo taken on a phone carries GPS coordinates, and a public catalogue would publish
    /// them"</em>. The assertion is on the bytes rather than on a library's opinion of them — what
    /// leaves this method is what a stranger downloads.
    /// </remarks>
    [Fact]
    public void Sanitise_APhotoCarryingWhatACameraWrote_KeepsNoneOfIt()
    {
        var uploaded = JpegCarrying(WhereItWasTaken, orientation: 1);

        // The fixture is only worth trusting if it really does carry the thing.
        Ascii(uploaded).Should().Contain(WhereItWasTaken);
        Ascii(uploaded).Should().Contain("Exif");

        var stored = _sanitiser
            .Sanitise(uploaded, TrainerPhoto.JpegContentType)
            .ShouldBeSuccess();

        Ascii(stored.Content.ToArray()).Should().NotContain(WhereItWasTaken);
        Ascii(stored.Content.ToArray()).Should().NotContain("Exif");
    }

    /// <summary>
    /// Sanitise, a portrait a camera recorded sideways, comes back upright.
    /// </summary>
    /// <remarks>
    /// The trap in every EXIF strip, and the reason this is not one line of code: the orientation
    /// tag <em>is</em> metadata, so removing the metadata without reading it first turns every
    /// portrait taken vertically onto its side. Orientation 6 says the stored pixels must be turned
    /// a quarter turn clockwise to be seen correctly, so a hundred by fifty becomes fifty by a
    /// hundred — and the result carries no orientation of its own, because it no longer needs one.
    /// </remarks>
    [Fact]
    public void Sanitise_APortraitRecordedSideways_ComesBackUpright()
    {
        var uploaded = JpegCarrying(WhereItWasTaken, orientation: 6, width: 100, height: 50);

        var stored = _sanitiser
            .Sanitise(uploaded, TrainerPhoto.JpegContentType)
            .ShouldBeSuccess();

        var (width, height, origin) = Describe(stored.Content.ToArray());

        width.Should().Be(50);
        height.Should().Be(100);
        origin.Should().Be(SKEncodedOrigin.TopLeft, "the turn has been applied, so nothing is left to record");
    }

    /// <summary>
    /// Sanitise, an image larger than any page shows, is bounded and keeps its ratio.
    /// </summary>
    /// <remarks>
    /// A transformation rather than a refusal: a twelve-megapixel portrait is a correct file, and
    /// answering an error to it would be this system's problem presented as the trainer's.
    /// </remarks>
    [Fact]
    public void Sanitise_AnImageLargerThanAnyPageShows_IsBoundedKeepingItsRatio()
    {
        var uploaded = JpegCarrying(WhereItWasTaken, orientation: 1, width: 2048, height: 1024);

        var stored = _sanitiser
            .Sanitise(uploaded, TrainerPhoto.JpegContentType)
            .ShouldBeSuccess();

        var (width, height, _) = Describe(stored.Content.ToArray());

        width.Should().Be(SkiaSharpPhotoSanitiser.MaxDimension);
        height.Should().Be(SkiaSharpPhotoSanitiser.MaxDimension / 2);
    }

    /// <summary>
    /// Sanitise, an image already within the bound, keeps its dimensions.
    /// </summary>
    [Fact]
    public void Sanitise_AnImageAlreadyWithinTheBound_KeepsItsDimensions()
    {
        var uploaded = JpegCarrying(WhereItWasTaken, orientation: 1, width: 200, height: 100);

        var stored = _sanitiser
            .Sanitise(uploaded, TrainerPhoto.JpegContentType)
            .ShouldBeSuccess();

        var (width, height, _) = Describe(stored.Content.ToArray());

        width.Should().Be(200);
        height.Should().Be(100);
    }

    /// <summary>
    /// Sanitise, a PNG, is answered a PNG.
    /// </summary>
    /// <remarks>
    /// The format survives on purpose. The aggregate has already established that the declared
    /// media type describes these bytes, and handing back a different format would make that
    /// agreement false one layer later — besides flattening a transparent background onto whatever
    /// colour the encoder chose.
    /// </remarks>
    [Fact]
    public void Sanitise_APng_IsAnsweredAPng()
    {
        var uploaded = Png(width: 40, height: 20);

        var stored = _sanitiser
            .Sanitise(uploaded, TrainerPhoto.PngContentType)
            .ShouldBeSuccess();

        stored.ContentType.Should().Be(TrainerPhoto.PngContentType);
        stored.Content.ToArray().Take(4).Should().Equal(0x89, (byte)'P', (byte)'N', (byte)'G');
    }

    /// <summary>
    /// Sanitise, bytes that carry a signature and do not decode, are refused by their own code.
    /// </summary>
    /// <remarks>
    /// The refusal only a decoder can produce: the aggregate's signature check passes these
    /// happily, because the first eight bytes really are a PNG's. <c>Trainer.PhotoUnreadable</c>
    /// tells a trainer their file is damaged, which is a different fix from the wrong format.
    /// </remarks>
    [Fact]
    public void Sanitise_BytesThatCarryASignatureAndDoNotDecode_AreRefusedByTheirOwnCode()
    {
        byte[] damaged = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[64]];

        _sanitiser
            .Sanitise(damaged, TrainerPhoto.PngContentType)
            .ShouldContainError(TrainerErrorCodes.PhotoUnreadable);
    }

    /// <summary>The width, height and recorded orientation of an encoded image.</summary>
    private static (int Width, int Height, SKEncodedOrigin Origin) Describe(byte[] content)
    {
        using var data = SKData.CreateCopy(content);
        using var stream = new SKMemoryStream(data);
        using var codec = SKCodec.Create(stream, out var opened);

        opened.Should().Be(SKCodecResult.Success);

        return (codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
    }

    private static string Ascii(byte[] content) => Encoding.ASCII.GetString(content);

    /// <summary>A JPEG carrying an EXIF block with a description and an orientation.</summary>
    private static byte[] JpegCarrying(
        string description,
        ushort orientation,
        int width = 100,
        int height = 50)
    {
        var jpeg = Encode(SKEncodedImageFormat.Jpeg, width, height);

        // Straight after the start-of-image marker, which is where a camera puts it.
        return [.. jpeg[..2], .. ExifSegment(description, orientation), .. jpeg[2..]];
    }

    private static byte[] Png(int width, int height) =>
        Encode(SKEncodedImageFormat.Png, width, height);

    /// <summary>
    /// An image with an asymmetric pattern, so that a turn is visible in its dimensions.
    /// </summary>
    private static byte[] Encode(SKEncodedImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 95);

        return encoded.ToArray();
    }

    /// <summary>
    /// An APP1 segment holding a little-endian TIFF block with two tags.
    /// </summary>
    /// <remarks>
    /// Written out rather than produced by a library, because the point of the fixture is that a
    /// reader can see exactly what the file carries. The layout is the one every camera writes: a
    /// byte-order mark, the magic 42, the offset of the first directory, then that directory's
    /// entries sorted by tag — a description stored past the end of the directory because it does
    /// not fit in four bytes, and an orientation that does.
    /// </remarks>
    private static byte[] ExifSegment(string description, ushort orientation)
    {
        var text = Encoding.ASCII.GetBytes(description + "\0");

        // Header (8) + entry count (2) + two entries (24) + next-directory offset (4).
        const uint DataOffset = 38;

        List<byte> tiff =
        [
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            .. BitConverter.GetBytes((ushort)2),
            .. Entry(tag: 0x010E, type: 2, count: (uint)text.Length, value: DataOffset),
            .. Entry(tag: 0x0112, type: 3, count: 1, value: orientation),
            .. BitConverter.GetBytes(0u),
            .. text
        ];

        List<byte> payload = [.. "Exif"u8, 0, 0, .. tiff];
        var length = payload.Count + 2;

        return [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. payload];
    }

    private static byte[] Entry(ushort tag, ushort type, uint count, uint value) =>
    [
        .. BitConverter.GetBytes(tag),
        .. BitConverter.GetBytes(type),
        .. BitConverter.GetBytes(count),
        .. BitConverter.GetBytes(value)
    ];
}
