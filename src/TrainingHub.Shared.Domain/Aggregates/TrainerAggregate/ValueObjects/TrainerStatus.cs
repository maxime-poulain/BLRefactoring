using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Whether a trainer is in good standing, or under sanction.
/// </summary>
/// <remarks>
/// <para>
/// Suspending a trainer writes this field and nothing else: their trainings are not touched, so
/// their catalogue leaves public view because its owner did and returns intact when the sanction is
/// lifted — with no record of which trainings the suspension hid, because it hid none. A sanction
/// that had to destroy a catalogue to take effect could not be undone. See ADR 0050.
/// </para>
/// <para>
/// A suspended trainer loses every write (ADR 0053). Three of those refusals live here, on the
/// aggregate that owns the rule — creating, publishing and transferring, giving and receiving
/// alike. The rest are refused at the boundary, before a use case runs, which is why this type
/// answers fewer questions than the sanction implies.
/// </para>
/// </remarks>
public sealed class TrainerStatus : ValueObject
{
    /// <summary>
    /// The trainer is in good standing. A trainer is born here.
    /// </summary>
    public static readonly TrainerStatus Active = new("Active");

    /// <summary>
    /// The trainer is under sanction: their catalogue is hidden and cannot grow.
    /// </summary>
    public static readonly TrainerStatus Suspended = new("Suspended");

    /// <summary>
    /// Every status there is, in declaration order.
    /// </summary>
    private static readonly IReadOnlyList<TrainerStatus> All = [Active, Suspended];

    /// <summary>
    /// The status's name, as the domain spells it and as it is persisted.
    /// </summary>
    public string Name { get; private init; } = null!;

    private TrainerStatus() { } // For ORM

    private TrainerStatus(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// The closed set of statuses.
    /// </summary>
    /// <returns>Every status a trainer can be in.</returns>
    public static IReadOnlyList<TrainerStatus> GetStatuses() => All;

    /// <summary>
    /// Resolves a status from the exact name stored beside it.
    /// </summary>
    /// <remarks>
    /// Throws on an unknown name, because a column holding a word this domain never wrote is a
    /// corrupt row rather than input to report back.
    /// <para>
    /// The ORM was the only caller until the administration got a filter to page trainers by
    /// standing (ADR 0055). That second caller hands over a name a <c>[KnownStatus]</c> annotation
    /// has already checked against <see cref="GetStatuses"/> at model binding, so an unknown one is
    /// a <c>400</c> naming the parameter and never arrives here — which is what lets this method go
    /// on throwing rather than answering a result.
    /// </para>
    /// </remarks>
    /// <param name="name">The persisted name.</param>
    /// <returns>The status that name spells.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The name is not one of the two.</exception>
    public static TrainerStatus FromName(string name)
        => All.FirstOrDefault(status => status.Name.Equals(name, StringComparison.Ordinal))
           ?? throw new ArgumentOutOfRangeException(
               nameof(name), name, "No trainer status answers to that name.");

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }
}
