using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// The portrait a trainer publishes on their profile.
/// </summary>
/// <remarks>
/// <para>
/// This carries an identity, a media type and a size — never a bucket, a key or a URL. The domain
/// says <em>which</em> photo a trainer has; the infrastructure decides where those bytes live and
/// how to name them. Swapping the storage technology therefore touches no file in this project,
/// which is the whole point of keeping the identifier here rather than the address.
/// </para>
/// <para>
/// <see cref="PhotoId"/> is fresh on every <see cref="Create"/>, and that is deliberate rather than
/// incidental: a replacement writes a <em>new</em> object and deletes the old one afterwards, so
/// the row never points at bytes that are not there yet. Reusing the identifier would mean
/// overwriting in place, which is the one ordering that can leave a profile pointing at nothing.
/// </para>
/// </remarks>
public sealed class TrainerPhoto : ValueObject
{
    /// <summary>
    /// The largest photo accepted, in bytes.
    /// </summary>
    /// <remarks>
    /// Five mebibytes is generous for a portrait and small enough that the whole thing can be held
    /// in memory while its signature is read. The HTTP layer repeats this number as a request size
    /// limit so an oversized upload is refused before it is buffered, not after.
    /// </remarks>
    public const int MaxSizeInBytes = 5 * 1024 * 1024;

    /// <summary>
    /// The media type for PNG.
    /// </summary>
    public const string PngContentType = "image/png";

    /// <summary>
    /// The media type for JPEG.
    /// </summary>
    public const string JpegContentType = "image/jpeg";

    /// <summary>
    /// The media type for WebP.
    /// </summary>
    public const string WebpContentType = "image/webp";

    /// <summary>
    /// Identifies these bytes. The infrastructure derives a storage key from it.
    /// </summary>
    public Guid PhotoId { get; private init; }

    /// <summary>
    /// The media type, established by reading the bytes rather than by trusting the caller.
    /// </summary>
    public string ContentType { get; private init; } = null!;

    /// <summary>
    /// How many bytes the photo occupies.
    /// </summary>
    public int ByteSize { get; private init; }

    private TrainerPhoto()
    {
    }

    /// <summary>
    /// Validates an uploaded image and describes it as a photo this domain will accept.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="declaredContentType">The media type the caller claims the bytes have.</param>
    /// <returns>The photo, or the reason it was refused.</returns>
    /// <remarks>
    /// <para>
    /// The declared type is checked <em>against the bytes</em>. An extension and a
    /// <c>Content-Type</c> header are both things a caller writes, so neither is evidence; the
    /// first bytes of the file are. A JPEG renamed <c>.png</c> is refused here, and so is anything
    /// whose signature this method does not recognise — SVG included, which matters because these
    /// photos are headed for a public catalogue and SVG is a script-carrying format.
    /// </para>
    /// <para>
    /// What this deliberately does <em>not</em> do is decode the image. Decoding would bound the
    /// dimensions and strip EXIF — and a photo taken on a phone carries GPS coordinates, which a
    /// public catalogue would publish. That hardening is recorded as deferred in ADR 0021 rather
    /// than left to be discovered; it needs an imaging library, and this method needs none.
    /// </para>
    /// </remarks>
    public static Result<TrainerPhoto> Create(ReadOnlySpan<byte> content, string? declaredContentType)
    {
        if (content.IsEmpty)
        {
            return Result<TrainerPhoto>.Failure(
                TrainerErrorCodes.PhotoEmpty, "A photo cannot be empty.");
        }

        if (content.Length > MaxSizeInBytes)
        {
            return Result<TrainerPhoto>.Failure(
                TrainerErrorCodes.PhotoTooLarge,
                $"A photo cannot exceed {MaxSizeInBytes} bytes.");
        }

        var declared = Normalise(declaredContentType);

        if (!IsSupported(declared))
        {
            return Result<TrainerPhoto>.Failure(
                TrainerErrorCodes.PhotoFormatNotSupported,
                $"A photo is one of {PngContentType}, {JpegContentType} or {WebpContentType}.");
        }

        var actual = DetectContentType(content);

        if (actual is null)
        {
            return Result<TrainerPhoto>.Failure(
                TrainerErrorCodes.PhotoFormatNotSupported,
                $"The uploaded bytes are none of {PngContentType}, {JpegContentType} or {WebpContentType}.");
        }

        if (!string.Equals(actual, declared, StringComparison.OrdinalIgnoreCase))
        {
            return Result<TrainerPhoto>.Failure(
                TrainerErrorCodes.PhotoContentMismatch,
                $"The upload is declared as {declared} but its content is {actual}.");
        }

        return Result<TrainerPhoto>.Success(new TrainerPhoto
        {
            PhotoId = Guid.NewGuid(),
            ContentType = actual,
            ByteSize = content.Length
        });
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PhotoId;
        yield return ContentType;
        yield return ByteSize;
    }

    /// <summary>
    /// Reads the leading bytes and names the format they belong to, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Three signatures, taken from the format specifications themselves. WebP is the awkward one:
    /// it is a RIFF container, so the four bytes that identify it sit at offset 8, past a length
    /// field that says nothing about the format.
    /// </remarks>
    private static string? DetectContentType(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(PngSignature))
        {
            return PngContentType;
        }

        if (content.StartsWith(JpegSignature))
        {
            return JpegContentType;
        }

        if (content.Length >= WebpHeaderLength
            && content.StartsWith(RiffSignature)
            && content[WebpMarkerOffset..WebpHeaderLength].SequenceEqual(WebpSignature))
        {
            return WebpContentType;
        }

        return null;
    }

    /// <summary>
    /// Reduces a header value to a bare media type: trimmed, with any parameter dropped.
    /// </summary>
    /// <remarks>
    /// A browser is entitled to send <c>image/jpeg; charset=binary</c>, and a comparison that has
    /// not dropped the parameter would reject it for a reason no user could act on. The case is
    /// left alone on purpose — media types are case-insensitive, so the comparisons below say so
    /// rather than folding the string first and having to justify which direction they folded it.
    /// </remarks>
    private static string Normalise(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var separator = contentType.IndexOf(';', StringComparison.Ordinal);
        var bare = separator < 0 ? contentType : contentType[..separator];

        return bare.Trim();
    }

    private static bool IsSupported(string contentType) =>
        string.Equals(contentType, PngContentType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, JpegContentType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, WebpContentType, StringComparison.OrdinalIgnoreCase);

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> RiffSignature => "RIFF"u8;

    private static ReadOnlySpan<byte> WebpSignature => "WEBP"u8;

    private const int WebpMarkerOffset = 8;

    private const int WebpHeaderLength = 12;
}
