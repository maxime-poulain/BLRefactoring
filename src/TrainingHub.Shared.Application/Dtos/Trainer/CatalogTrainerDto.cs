using TrainingHub.Shared.Application.Dtos.Training;

namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// A trainer as their public profile reads them: who they are, and what they offer (ADR 0070).
/// </summary>
/// <remarks>
/// The first shape a trainer takes for nobody in particular, and deliberately the smallest. It
/// carries what a visitor deciding whether to learn from this person can use — a name, a bio, a
/// face, the offered trainings — and none of what the authenticated shapes carry: no e-mail, no
/// status, no reason. A profile that answers at all belongs to a trainer with something on offer,
/// so the states that would need explaining are exactly the states this shape never reaches.
/// <para>
/// The trainings arrive as catalog rows rather than as anything richer, because that is what they
/// are: each one is a way of finding a training, and the reading stays where it always was, one
/// navigation away. The list is bounded by the domain rather than paged by the contract — a trainer
/// offers at most as many trainings as the quota allows — so a page here would be ceremony around
/// a list that cannot grow past it.
/// </para>
/// <para>
/// The portrait arrives as a <em>photo's</em> identifier, exactly as the training detail hands it
/// out: the address a client builds from it names this trainer and that photo, stays valid for as
/// long as those bytes exist, and answers nothing for a portrait that carries no proof of having
/// been stripped (ADR 0063).
/// </para>
/// </remarks>
public sealed class CatalogTrainerDto
{
    /// <summary>The trainer's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer's first name.</summary>
    public required string Firstname { get; init; }

    /// <summary>The trainer's last name.</summary>
    public required string Lastname { get; init; }

    /// <summary>
    /// What the trainer says about themselves, or <see langword="null"/> when they say nothing.
    /// </summary>
    public string? Bio { get; init; }

    /// <summary>
    /// The portrait to show beside the trainer's name, or <see langword="null"/> when there is none
    /// a visitor may see.
    /// </summary>
    /// <remarks>
    /// Null for two different situations, and the reading is deliberately the same: the trainer has
    /// no photo, or they have one stored before anything stripped it (ADR 0063). Either way the
    /// page shows a name and no face.
    /// </remarks>
    public Guid? PhotoId { get; init; }

    /// <summary>The trainings the trainer offers, alphabetically by title.</summary>
    public required IReadOnlyList<CatalogTrainingDto> Trainings { get; init; }
}
