using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.Application.Photos;

/// <summary>
/// Turns the bytes a trainer uploaded into the bytes this system is willing to publish (ADR 0063).
/// </summary>
/// <remarks>
/// ADR 0021 refused to decode an uploaded portrait and recorded what that cost: <em>"a photo taken
/// on a phone carries GPS coordinates, and a public catalogue would publish them"</em>. It also
/// named the library that would fix it and called this <em>"the one decision here that gets more
/// expensive with time"</em>. This port is that decision taken.
/// <para>
/// It is not a rule and it is not a question — it is a transformation, which is why it does not
/// live beside the aggregate the way <c>IUniquenessTitleChecker</c> does (ADR 0030). The domain
/// says what counts as a photo; this says what the stored bytes are. What comes back is decoded,
/// oriented, bounded and re-encoded with no metadata of any kind.
/// </para>
/// <para>
/// <b>It runs before the aggregate validates, and that order is the design.</b> The aggregate
/// records a media type and a byte count, and those have to describe the bytes that were actually
/// stored — sanitising afterwards would leave <c>TrainerPhoto.ByteSize</c> describing an upload
/// nobody kept.
/// </para>
/// </remarks>
public interface IPhotoSanitiser
{
    /// <summary>
    /// Strips an uploaded image, or says why it could not be read.
    /// </summary>
    /// <remarks>
    /// The refusal is <c>Trainer.PhotoUnreadable</c> and it is narrow: bytes that carry a supported
    /// signature and then fail to decode. Everything else about what counts as a photo — the
    /// formats, the size, the declared type matching the content — stays the aggregate's, and is
    /// checked on the result of this rather than on the upload.
    /// </remarks>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="contentType">The media type read off those bytes.</param>
    /// <returns>The bytes to store, or why the upload could not be read.</returns>
    Result<SanitisedPhoto> Sanitise(ReadOnlyMemory<byte> content, string contentType);
}
