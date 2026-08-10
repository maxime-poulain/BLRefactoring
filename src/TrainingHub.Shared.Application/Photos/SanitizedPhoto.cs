namespace TrainingHub.Shared.Application.Photos;

/// <summary>
/// An image as it will be stored: decoded, oriented, bounded, re-encoded, and carrying nothing a
/// camera wrote (ADR 0063).
/// </summary>
/// <param name="Content">The bytes to store, which are not the bytes that were uploaded.</param>
/// <param name="ContentType">Their media type, which is the uploaded one — the format is
/// preserved on purpose, so that a trainer who sends a PNG is not handed back a JPEG.</param>
/// <remarks>
/// A record rather than a bare <c>byte[]</c> so that the pair travels together: the caller hands
/// both to <c>TrainerPhoto.Create</c>, and a signature that returned only the bytes would invite
/// somebody to validate the new bytes against the old declaration.
/// </remarks>
public sealed record SanitizedPhoto(ReadOnlyMemory<byte> Content, string ContentType);
