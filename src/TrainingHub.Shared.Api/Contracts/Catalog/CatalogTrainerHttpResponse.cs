namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// The body of <c>GET /Catalog/trainers/{id}</c>: one offering trainer's public page (ADR 0070).
/// </summary>
/// <remarks>
/// The page <see cref="CatalogTrainingHttpResponse"/> waited for: a search row hands out a
/// trainer's identifier, and this is what that identifier now resolves to. It answers only for a
/// person with at least one training on offer — offered or invisible, with no state in between —
/// so it carries none of what the authenticated contracts carry: no e-mail, no status, no reason.
/// A profile that answers at all has nothing to explain.
/// <para>
/// The bio is the one disclosure this contract makes that nothing published before: it was written
/// by the trainer for their profile, and until this page existed there was no profile to put it
/// on. It leaves as the plain text the domain bounded, and rendering it safely is the reader's
/// job, as it is for every other string here.
/// </para>
/// <para>
/// The portrait arrives as a <em>photo's</em> identifier, exactly as the training detail hands it
/// out: the address a client builds from it names this trainer and that photo, and answers
/// nothing for a portrait that carries no proof of having been stripped (ADR 0063).
/// </para>
/// </remarks>
public sealed class CatalogTrainerHttpResponse
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
    /// The portrait to show beside the trainer's name, or <see langword="null"/> when there is
    /// none a visitor may see.
    /// </summary>
    /// <remarks>
    /// Optional rather than required, and null covers both "no photo" and "a photo nothing can
    /// prove was stripped" — a distinction a visitor has no use for.
    /// </remarks>
    public Guid? PhotoId { get; init; }

    /// <summary>The trainings the trainer offers, alphabetically by title.</summary>
    /// <remarks>
    /// Rows of the same shape the search answers, because that is what they are: ways of finding a
    /// training, each one navigation away from its reading. Bounded by the domain's quota rather
    /// than paged — a list that cannot outgrow one screen needs no page protocol.
    /// </remarks>
    public required IReadOnlyList<CatalogTrainingHttpResponse> Trainings { get; init; }
}
