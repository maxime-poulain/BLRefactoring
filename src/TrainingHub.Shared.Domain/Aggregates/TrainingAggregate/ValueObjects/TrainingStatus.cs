using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Where a training stands in its owner's catalogue: offered to the public, or withdrawn from it.
/// </summary>
/// <remarks>
/// <para>
/// Two states and no third. There is no <c>Draft</c> because there is no drafting — creation takes
/// five required fields and produces a complete training in one call, so a state nothing can remain
/// in is not a state. The pair is reachable in both directions, which is what makes this a lifecycle
/// rather than a tombstone wearing an enum: see ADR 0050.
/// </para>
/// <para>
/// A class rather than a C# <see langword="enum"/>, like <see cref="Topic"/> beside it: the
/// ubiquitous language names a type, and a field of an enum is not one.
/// </para>
/// </remarks>
public sealed class TrainingStatus : ValueObject
{
    /// <summary>
    /// The training is offered. A training is born here.
    /// </summary>
    public static readonly TrainingStatus Published = new("Published");

    /// <summary>
    /// The training has been withdrawn by its owner. It keeps its title and frees its place in the
    /// catalogue's quota, and its owner may publish it again.
    /// </summary>
    public static readonly TrainingStatus Unpublished = new("Unpublished");

    /// <summary>
    /// Every status there is, in declaration order.
    /// </summary>
    /// <remarks>
    /// Declared rather than reflected out of the static fields, for the reason
    /// <see cref="Topic"/> records: a closed enumeration that discovers itself is a closed
    /// enumeration one caller can reopen.
    /// </remarks>
    private static readonly IReadOnlyList<TrainingStatus> All = [Published, Unpublished];

    /// <summary>
    /// The status's name, as the domain spells it and as it is persisted.
    /// </summary>
    public string Name { get; private init; } = null!;

    private TrainingStatus() { } // For ORM

    private TrainingStatus(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// The closed set of statuses.
    /// </summary>
    /// <returns>Every status a training can be in.</returns>
    public static IReadOnlyList<TrainingStatus> GetStatuses() => All;

    /// <summary>
    /// Resolves a status from the exact name stored beside it.
    /// </summary>
    /// <remarks>
    /// Throws rather than answering a result, unlike <see cref="Topic.TryFromName"/>, because the
    /// callers are not the same. A topic name arrives from a client and is reported back as a
    /// validation error along with everything else that was wrong; a status name arrives from the
    /// column this type was written to, so a value the domain does not know means the row is
    /// corrupt — which no caller can be asked to handle and none should silently read as
    /// <see cref="Published"/>.
    /// </remarks>
    /// <param name="name">The persisted name.</param>
    /// <returns>The status that name spells.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The name is not one of the two.</exception>
    public static TrainingStatus FromName(string name)
        => All.FirstOrDefault(status => status.Name.Equals(name, StringComparison.Ordinal))
           ?? throw new ArgumentOutOfRangeException(
               nameof(name), name, "No training status answers to that name.");

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }
}
