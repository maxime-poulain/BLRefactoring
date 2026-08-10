using System.Text;
using SkiaSharp;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Real, decodable portraits for the suites that upload one.
/// </summary>
/// <remarks>
/// A signature followed by zeroes was enough until ADR 0063: it carried a media type, which was all
/// the aggregate read. These endpoints now decode what they are sent, so an upload that is not an
/// image is answered <c>Trainer.PhotoUnreadable</c> — and a suite still sending one would be
/// asserting the wrong refusal.
/// <para>
/// Shared rather than written twice, because two copies of "how to make a picture" is exactly the
/// kind of thing that drifts into one suite testing something the other does not.
/// </para>
/// </remarks>
public static class Portraits
{
    /// <summary>
    /// A small square portrait, encoded in the format the media type names.
    /// </summary>
    /// <param name="contentType">The media type to encode as. Anything but JPEG is written as PNG.</param>
    /// <param name="side">How many pixels each side has.</param>
    /// <returns>The encoded bytes.</returns>
    public static byte[] Of(string contentType, int side = 32)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(side, side, SKColorType.Rgba8888, SKAlphaType.Premul));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0x33, 0x66, 0x99));
        }

        using var image = SKImage.FromBitmap(bitmap);

        var format = contentType == TrainerPhoto.JpegContentType
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;

        using var encoded = image.Encode(format, 90);

        return encoded.ToArray();
    }

    /// <summary>
    /// A JPEG carrying an EXIF block whose description holds a recognisable marker.
    /// </summary>
    /// <param name="marker">The text written into the description tag.</param>
    /// <returns>The encoded bytes, with the segment where a camera puts it.</returns>
    /// <remarks>
    /// For the one fact that can only be asserted over HTTP: that the pipeline calls the sanitiser
    /// at all. Whether the sanitiser strips is proved on real bytes in its own unit tests; what a
    /// round trip through the API adds is that nothing between the controller and the object store
    /// forgot to run it, and a marker surviving is how that regression would look.
    /// </remarks>
    public static byte[] JpegCarrying(string marker)
    {
        var jpeg = Of(TrainerPhoto.JpegContentType);

        // Straight after the start-of-image marker, which is where a camera puts it. The signature
        // the aggregate reads is the first three bytes, so this is still a JPEG to Vet.
        return [.. jpeg[..2], .. ExifSegment(marker), .. jpeg[2..]];
    }

    /// <summary>
    /// An APP1 segment holding a little-endian TIFF block with one tag.
    /// </summary>
    /// <remarks>
    /// Written out rather than produced by a library, so a reader can see exactly what the upload
    /// carries: a byte-order mark, the magic 42, the offset of the first directory, then one entry
    /// — a description, stored past the end of the directory because it does not fit in four bytes.
    /// </remarks>
    private static byte[] ExifSegment(string description)
    {
        var text = Encoding.ASCII.GetBytes(description + "\0");

        // Header (8) + entry count (2) + one entry (12) + next-directory offset (4).
        const uint DataOffset = 26;

        List<byte> tiff =
        [
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            .. BitConverter.GetBytes((ushort)1),
            .. BitConverter.GetBytes((ushort)0x010E),
            .. BitConverter.GetBytes((ushort)2),
            .. BitConverter.GetBytes((uint)text.Length),
            .. BitConverter.GetBytes(DataOffset),
            .. BitConverter.GetBytes(0u),
            .. text
        ];

        List<byte> payload = [.. "Exif"u8, 0, 0, .. tiff];
        var length = payload.Count + 2;

        return [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. payload];
    }
}
