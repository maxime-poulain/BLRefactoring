using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// The body of <c>POST /Catalog/trainers/{trainerId}/contact</c>.
/// </summary>
/// <remarks>
/// Everything here belongs to the visitor: who they are, where they want to be answered, what they
/// wrote, and which training prompted them. Who receives it is the route's business, and what the
/// recipient's address is never appears on this boundary at all — the message reaches them without
/// the browser ever being told where it went (ADR 0082).
/// <para>
/// <b><see cref="SenderEmailAddress"/> is the visitor's, and the distinction is load-bearing.</b>
/// <c>NoCatalogContract_CarriesAPrivateMember</c> refuses an address on what the catalog
/// <em>answers</em>, because the trainer's address is a fact the catalog withholds (ADR 0070). An
/// address a caller supplies about themself is the opposite kind of thing, and ADR 0082 narrows
/// that rule to the answering half rather than letting a name collision decide.
/// </para>
/// <para>
/// Presence and length only, as everywhere on this boundary: whether the address is shaped like one
/// is the domain's judgment, not <c>[EmailAddress]</c>'s, and the two disagree in both directions
/// (ADR 0043).
/// </para>
/// </remarks>
public sealed class ContactTrainerHttpRequest
{
    /// <summary>The visitor's first name, as they gave it.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string SenderFirstname { get; init; }

    /// <summary>The visitor's last name, as they gave it.</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string SenderLastname { get; init; }

    /// <summary>
    /// The address the visitor asks to be answered at, which becomes the message's <c>Reply-To</c>.
    /// </summary>
    /// <remarks>
    /// Bounded at the width the domain stores an address in, so a value no mailbox could have is
    /// refused at the door rather than after a round trip.
    /// </remarks>
    [Required]
    [StringLength(320, MinimumLength = 1)]
    public required string SenderEmailAddress { get; init; }

    /// <summary>What the visitor wants to say.</summary>
    /// <remarks>
    /// The upper bound is the one thing standing between a contact form and a way to post a
    /// megabyte into somebody's mailbox; the lower one refuses an empty message, which is a click
    /// rather than a contact.
    /// </remarks>
    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public required string Message { get; init; }

    /// <summary>
    /// The training the visitor was reading, or <see langword="null"/> when they wrote from the
    /// trainer's own page.
    /// </summary>
    /// <remarks>
    /// It names the message rather than addressing it: the recipient is the trainer either way, and
    /// this only lets the notice say what prompted it. Nullable, so
    /// <see cref="NotEmptyIdentifierAttribute"/> and the absence of a value compose rather than
    /// answering for the same thing (ADR 0046).
    /// </remarks>
    [NotEmptyIdentifier]
    public Guid? TrainingId { get; init; }

    /// <summary>
    /// Left empty by anyone who can see the form.
    /// </summary>
    /// <remarks>
    /// A honeypot, and the reason it is named for something plausible rather than for what it is: a
    /// field called <c>Honeypot</c> teaches a bot to skip it. It is rendered hidden and out of the
    /// tab order, so a person never reaches it and a form-filling script does. A request that
    /// carries one is answered exactly as a good one is — accepted, and dropped without sending —
    /// because a different answer is how a bot finds out (ADR 0082).
    /// </remarks>
    [StringLength(200)]
    public string? Website { get; init; }
}
