using SkiaSharp;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Infrastructure.Photos;

/// <inheritdoc />
/// <remarks>
/// The one place in this repository that decodes an image, and an architecture rule holds that line
/// for the same reason it holds it for S3 and SMTP: a decoder is a large surface reading hostile
/// input, and knowing where it runs is most of knowing what it can do to you.
/// <para>
/// Re-encoding is what strips the metadata. There is no "remove the EXIF block" call here and there
/// should not be: an allow-list of segments to keep is a list somebody has to maintain against every
/// format, while decoding to pixels and encoding again keeps nothing by construction.
/// </para>
/// </remarks>
public sealed class SkiaSharpPhotoSanitiser : IPhotoSanitiser
{
    /// <summary>
    /// The longest side a stored portrait may have.
    /// </summary>
    /// <remarks>
    /// ADR 0021 bounded the upload in bytes because that is all a signature check can see. This is
    /// the bound it said decoding would allow, and it is a <em>transformation</em> rather than a
    /// refusal: a photographer's twelve-megapixel portrait is not a bad upload, it is simply larger
    /// than any page will ever show. Refusing it would make a correct file an error.
    /// </remarks>
    public const int MaxDimension = 1024;

    /// <summary>
    /// The quality lossy formats are re-encoded at.
    /// </summary>
    /// <remarks>
    /// Ninety is the usual point where a further increase costs bytes and shows nothing. It applies
    /// to JPEG and WebP; PNG ignores it, being lossless.
    /// </remarks>
    private const int Quality = 90;

    /// <summary>
    /// How pixels are sampled when a portrait is shrunk. Nearest would alias badly at these ratios.
    /// </summary>
    private static readonly SKSamplingOptions Smooth =
        new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// How pixels are sampled when a portrait is only turned. There is nothing to interpolate — the
    /// transform is a right angle or a mirror — and asking for smoothing would soften every photo
    /// a camera happened to record sideways.
    /// </summary>
    private static readonly SKSamplingOptions Exact = new(SKFilterMode.Nearest);

    /// <inheritdoc />
    public Result<SanitisedPhoto> Sanitise(ReadOnlyMemory<byte> content, string contentType)
    {
        using var data = SKData.CreateCopy(content.Span);
        using var stream = new SKMemoryStream(data);

        // SKCodec rather than SKBitmap.Decode, and this is the whole reason: only the codec exposes
        // the orientation the camera recorded. Decoding straight to a bitmap silently discards it,
        // which would land a portrait taken vertically on its side — the metadata removed correctly
        // and the picture wrong.
        //
        // The overload with a result is what says whether the bytes are readable. Testing the
        // returned reference for null would read the same at run time and is dead code to the
        // analyser, which annotates it as never null.
        using var codec = SKCodec.Create(stream, out var opened);

        if (opened is not SKCodecResult.Success)
        {
            return Unreadable();
        }

        var origin = codec.EncodedOrigin;

        // One predictable pixel layout rather than whatever the file happened to carry: every
        // format this domain accepts survives it, and it keeps the transforms below from having to
        // reason about a colour type per format.
        using var decoded = new SKBitmap(new SKImageInfo(
            codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));

        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) is not SKCodecResult.Success)
        {
            return Unreadable();
        }

        using var oriented = Orient(decoded, origin);
        using var bounded = Bound(oriented);
        using var image = SKImage.FromBitmap(bounded);
        using var encoded = image.Encode(FormatOf(contentType), Quality);

        return Result<SanitisedPhoto>.Success(new SanitisedPhoto(encoded.ToArray(), contentType));
    }

    private static Result<SanitisedPhoto> Unreadable() =>
        Result<SanitisedPhoto>.Failure(
            TrainerErrorCodes.PhotoUnreadable,
            "The upload carries the signature of an image this domain accepts, and does not decode.");

    /// <summary>
    /// The format to write back, which is the format that came in.
    /// </summary>
    /// <remarks>
    /// Preserved rather than normalised to one format. A trainer who uploads a PNG with a
    /// transparent background and is handed back a JPEG on a white square would be right to call
    /// that a bug, and the aggregate has already established that this string describes the bytes.
    /// </remarks>
    private static SKEncodedImageFormat FormatOf(string contentType) => contentType switch
    {
        TrainerPhoto.PngContentType => SKEncodedImageFormat.Png,
        TrainerPhoto.WebpContentType => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Jpeg
    };

    /// <summary>
    /// Applies the orientation the camera recorded, so that dropping it loses nothing.
    /// </summary>
    /// <remarks>
    /// The eight EXIF origins say how the stored pixels must be transformed to be seen the right way
    /// up. Four of them swap the axes, so the destination is the source with its sides exchanged.
    /// The matrix maps source coordinates into that destination:
    /// <c>x' = ScaleX·x + SkewX·y + TransX</c> and <c>y' = SkewY·x + ScaleY·y + TransY</c>.
    /// </remarks>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }

        float width = source.Width;
        float height = source.Height;

        var swapsAxes = origin
            is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var oriented = new SKBitmap(new SKImageInfo(
            swapsAxes ? source.Height : source.Width,
            swapsAxes ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType));

        var matrix = origin switch
        {
            SKEncodedOrigin.TopLeft => SKMatrix.Identity,
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };

        using var canvas = new SKCanvas(oriented);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0, Exact, paint: null);

        return oriented;
    }

    /// <summary>
    /// Shrinks a portrait whose longest side is larger than a page will ever show.
    /// </summary>
    /// <remarks>
    /// The ratio is kept, and an image already within the bound is copied rather than resampled —
    /// running every upload through a resampler would cost quality for nothing.
    /// </remarks>
    private static SKBitmap Bound(SKBitmap source)
    {
        var longest = Math.Max(source.Width, source.Height);

        if (longest <= MaxDimension)
        {
            return source.Copy();
        }

        var scale = (double)MaxDimension / longest;

        var target = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            source.ColorType,
            source.AlphaType);

        return source.Resize(target, Smooth);
    }
}
