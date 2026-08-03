namespace TrainingHub.Shared.Common;

/// <summary>
/// Thrown when the store rejected a change because the aggregate had been modified
/// by someone else since it was read.
/// </summary>
/// <remarks>
/// A storage-agnostic translation of the provider's optimistic-concurrency failure,
/// so the application layer never has to know that Entity Framework or SQL Server
/// are involved — the same role <see cref="UniqueConstraintViolationException"/>
/// plays for a violated unique index.
/// </remarks>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
