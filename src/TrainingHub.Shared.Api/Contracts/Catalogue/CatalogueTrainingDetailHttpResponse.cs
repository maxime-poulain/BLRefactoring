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
/// that is not answers 404. No portrait either, and that one is a precondition rather than a
/// preference — ADR 0021 records that a photograph taken on a phone carries the coordinates of
/// where it was taken, and nothing here strips them yet.
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
}
