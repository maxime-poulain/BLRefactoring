namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// A trainer's photo, as the read side hands it out.
/// </summary>
/// <param name="PhotoId">Identifies these bytes, and is what the HTTP layer turns into an ETag.</param>
/// <param name="Content">The bytes.</param>
/// <param name="ContentType">Their media type.</param>
/// <remarks>
/// A record rather than a stream, for the same reason <c>StoredObject</c> is: the payload is
/// bounded, and a stream would hand the caller a lifetime to manage in exchange for nothing at
/// this size. It is a DTO because a query answers with one — never with an aggregate or a value
/// object — which is what keeps the read side from leaking the write model.
/// </remarks>
public sealed record TrainerPhotoDto(Guid PhotoId, ReadOnlyMemory<byte> Content, string ContentType);
