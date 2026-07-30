namespace BLRefactoring.Shared.Common;

/// <summary>
/// Thrown by the unit of work when the database rejects a save because of a
/// unique constraint or unique index violation.
/// </summary>
/// <remarks>
/// This is the storage-agnostic translation of a provider-specific error:
/// callers that guard an invariant with a unique index (a lost race against a
/// concurrent request slipping past an application-level pre-check) can catch
/// this exception and turn it into an ordinary business failure, without ever
/// depending on the persistence technology.
/// </remarks>
public sealed class UniqueConstraintViolationException(string message, Exception innerException)
    : Exception(message, innerException);
