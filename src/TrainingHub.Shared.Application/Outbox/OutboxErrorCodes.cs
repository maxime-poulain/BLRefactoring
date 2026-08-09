using TrainingHub.Shared.Common.Errors;

namespace TrainingHub.Shared.Application.Outbox;

/// <summary>
/// The codes the outbox's operator surface raises.
/// </summary>
/// <remarks>
/// A holder that names no aggregate, because the outbox is not one: it is the platform's own table,
/// and the refusal below is about a row's delivery state rather than about a business rule. It still
/// follows ADR 0015's shape — declared on a holder, prefixed with what owns it, never built at a
/// call site — so the vocabulary stays greppable whoever raises it (ADR 0061).
/// <para>
/// A message the caller named but that does not exist answers the kernel's
/// <see cref="ErrorCodes.NotFound"/> instead: "there is no such row" is true of anything with an
/// identifier, and naming an owner for it would be inventing one.
/// </para>
/// </remarks>
public static class OutboxErrorCodes
{
    /// <summary>
    /// The message exists, and is not poison: still owed, or already delivered.
    /// </summary>
    /// <remarks>
    /// Requeuing either would be a different operation than the one this surface offers. A message
    /// still inside its budget is already scheduled — putting it back would move its next attempt
    /// forward, which is a decision nobody asked for — and a delivered message has consumers that
    /// settled, so replaying it would undo the guarantee the ledger exists to give.
    /// </remarks>
    public static readonly ErrorCode NotPoison = new("Outbox.NotPoison");
}
