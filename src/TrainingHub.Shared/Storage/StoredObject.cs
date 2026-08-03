namespace TrainingHub.Shared.Storage;

/// <summary>
/// The bytes an <see cref="IObjectStore"/> holds under one key, and what they are.
/// </summary>
/// <param name="Content">The bytes.</param>
/// <param name="ContentType">The media type they were stored with.</param>
/// <remarks>
/// Buffered rather than streamed. Everything this store holds is bounded — a portrait is capped at
/// a few mebibytes — so handing back an array keeps callers, tests and the HTTP layer free of
/// stream lifetimes that would buy nothing at this size.
/// </remarks>
public sealed record StoredObject(ReadOnlyMemory<byte> Content, string ContentType);
