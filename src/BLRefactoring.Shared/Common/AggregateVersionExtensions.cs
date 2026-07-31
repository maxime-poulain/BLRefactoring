namespace BLRefactoring.Shared.Common;

/// <summary>
/// Helpers for comparing the version an aggregate was read at.
/// </summary>
public static class AggregateVersionExtensions
{
    /// <summary>
    /// Tells whether the aggregate is still at the version the caller read.
    /// </summary>
    /// <remarks>
    /// This is the check that catches the case the store cannot see: two users
    /// editing from a form they loaded at different times. Each request reloads the
    /// aggregate, so by the time the second one saves, the store's own token already
    /// matches — only the version the caller carried from their read reveals that
    /// they are about to overwrite someone else's work.
    /// It is a pre-check, not the guarantee: two requests can both pass it and race
    /// to the update. The concurrency token in the database settles that race, and
    /// both paths surface the same business error.
    /// </remarks>
    public static bool IsAtVersion(this IAggregateRoot aggregate, byte[] expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(expectedVersion);

        return aggregate.RowVersion.AsSpan().SequenceEqual(expectedVersion);
    }
}
