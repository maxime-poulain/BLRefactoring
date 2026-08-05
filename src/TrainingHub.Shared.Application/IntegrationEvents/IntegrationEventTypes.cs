namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// The stable wire identity of every integration event: name and version on one side, CLR type on
/// the other.
/// </summary>
/// <remarks>
/// Written out rather than derived. A CLR type name changes with a refactoring; a wire name written
/// in this table changes only when somebody edits the table, which is what makes it safe to store
/// and read back years later. The map is deliberately not built by scanning an assembly — an
/// implicit convention is exactly the kind of decision nobody reviews — and its completeness is
/// enforced by an architecture rule rather than at first use
/// (<c>EveryIntegrationEvent_HasAStableName</c>).
/// <para>
/// Versioning: an additive, non-breaking payload change keeps its entry; a breaking change
/// registers the new shape under a bumped version, and the old entry stays until no stored message
/// carries it (ADR 0024).
/// </para>
/// </remarks>
public static class IntegrationEventTypes
{
    private static readonly IReadOnlyDictionary<Type, (string Name, int Version)> Registered =
        new Dictionary<Type, (string, int)>
        {
            [typeof(TrainerCreatedIntegrationEvent)] = ("TrainerCreated", 1),
            [typeof(TrainerContactEmailChangedIntegrationEvent)] = ("TrainerContactEmailChanged", 1),
            [typeof(TrainingCreatedIntegrationEvent)] = ("TrainingCreated", 1),
            [typeof(TrainingEditedIntegrationEvent)] = ("TrainingEdited", 1),
            [typeof(TrainingTransferredIntegrationEvent)] = ("TrainingTransferred", 1),
        };

    /// <summary>
    /// Every registered integration event type — what the completeness rule and the round-trip
    /// tests enumerate.
    /// </summary>
    public static IReadOnlyCollection<Type> All => [.. Registered.Keys];

    /// <summary>
    /// The stable name and version under which <paramref name="type"/> travels.
    /// </summary>
    /// <param name="type">The integration event type to look up.</param>
    public static (string Name, int Version) NameOf(Type type) =>
        Registered.TryGetValue(type, out var identity)
            ? identity
            : throw new InvalidOperationException(
                $"{type.Name} is not registered in {nameof(IntegrationEventTypes)}. Every integration " +
                "event carries a stable wire name declared here — add an entry, and the architecture " +
                "rule that should have caught this earlier will tell you if the two ever drift again.");

    /// <summary>
    /// The CLR type a stored message with this <paramref name="name"/> and
    /// <paramref name="version"/> deserializes into — the delivery worker's read path.
    /// </summary>
    /// <param name="name">The stable name found on the envelope.</param>
    /// <param name="version">The version found on the envelope.</param>
    public static Type Resolve(string name, int version) =>
        Registered.SingleOrDefault(entry => entry.Value == (name, version)).Key
        ?? throw new InvalidOperationException(
            $"No integration event is registered as '{name}' v{version}. Either the message was " +
            "written by a newer schema than this code knows, or an entry was removed while stored " +
            "messages still carry it — entries outlive the events they name (ADR 0024).");
}
