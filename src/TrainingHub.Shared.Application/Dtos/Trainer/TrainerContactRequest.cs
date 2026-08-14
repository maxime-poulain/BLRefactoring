namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// What a visitor is asking to have carried to a trainer (ADR 0082).
/// </summary>
/// <remarks>
/// The layered stack's input, so it is a <c>*Request</c> and never a <c>*HttpRequest</c>: the
/// qualifier says which boundary a type belongs to, and this one is behind the transport
/// (ADR 0048). Its CQRS counterpart is <c>ContactTrainerCommand</c>, carrying the same six fields
/// under a different shape.
/// <para>
/// Every field is the visitor's own. The trainer's address is not here and could not be: it is read
/// at delivery, in the one consumer allowed to know it.
/// </para>
/// </remarks>
public sealed class TrainerContactRequest
{
    /// <summary>The trainer the message is addressed to.</summary>
    public required Guid TrainerId { get; init; }

    /// <summary>
    /// The training the visitor was reading, or <see langword="null"/> when they wrote from the
    /// trainer's own page.
    /// </summary>
    public Guid? TrainingId { get; init; }

    /// <summary>The visitor's first name, as they gave it.</summary>
    public required string SenderFirstname { get; init; }

    /// <summary>The visitor's last name, as they gave it.</summary>
    public required string SenderLastname { get; init; }

    /// <summary>The address the visitor asks to be answered at.</summary>
    public required string SenderEmailAddress { get; init; }

    /// <summary>What the visitor wrote.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// Whether the honeypot came back filled, in which case nothing is sent (ADR 0082).
    /// </summary>
    /// <remarks>
    /// The verdict travels rather than the value: what a bot typed is of no interest to any layer
    /// below the boundary, and carrying the string would invite somebody to start reading it.
    /// </remarks>
    public bool LooksAutomated { get; init; }
}
