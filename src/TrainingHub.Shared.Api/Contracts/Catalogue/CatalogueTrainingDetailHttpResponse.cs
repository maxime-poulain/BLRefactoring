namespace TrainingHub.Shared.Api.Contracts.Catalogue;

/// <summary>
/// The body of <c>GET /Catalogue/trainings/{id}</c>: one offered training, read in full (ADR 0062).
/// </summary>
/// <remarks>
/// The answer <see cref="CatalogueTrainingHttpResponse"/> deliberately does not give. That row is
/// <em>a way of finding a training rather than a way of reading it</em>, and three members is the
/// right size for a search result; this is what a visitor gets when they follow one.
/// <para>
/// It names the trainer where the row identifies them, and that is not the reversal it looks like.
/// The row's argument was that <em>publishing every trainer's name to anybody is precisely the read
/// this API withdrew</em> — a listing of names, obtainable by paging. One name, attached to one
/// training the trainer chose to publish, is the authorship of that training rather than a
/// directory of people: an offered course whose author is a GUID is a page nobody can use.
/// </para>
/// <para>
/// No status, for the same reason as the row: a training that answers here is on offer, and one
/// that is not answers 404. The portrait arrives as an identifier rather than as bytes, and it is
/// a <em>photo's</em> identifier — the address a client builds from it names this training and that
/// photo, so nothing here hands out a way to address a person. It waited on the precondition
/// ADR 0021 recorded, that a photograph taken on a phone carries the coordinates of where it was
/// taken; ADR 0063 strips them and refuses to publish what it cannot prove was stripped.
/// </para>
/// </remarks>
public sealed class CatalogueTrainingDetailHttpResponse
{
    /// <summary>The training's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The training's title.</summary>
    public required string Title { get; init; }

    /// <summary>The trainer who offers it, named rather than identified.</summary>
    public required string TrainerName { get; init; }

    /// <summary>The topics it is filed under.</summary>
    public required IReadOnlyList<string> Topics { get; init; }

    /// <summary>What the training is about.</summary>
    public required string Description { get; init; }

    /// <summary>What a participant is expected to bring.</summary>
    public required string Prerequisites { get; init; }

    /// <summary>What a participant leaves with.</summary>
    public required string AcquiredSkills { get; init; }

    /// <summary>
    /// The portrait to show beside the trainer's name, or <see langword="null"/> when there is none
    /// a visitor may see.
    /// </summary>
    /// <remarks>
    /// Optional rather than required, and null covers both "no photo" and "a photo nothing can
    /// prove was stripped" — a distinction a visitor has no use for.
    /// </remarks>
    public Guid? TrainerPhotoId { get; init; }
}
