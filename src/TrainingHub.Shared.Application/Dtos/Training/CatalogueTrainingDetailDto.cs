namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// One offered training, as a visitor reads it (ADR 0062).
/// </summary>
/// <remarks>
/// The fourth shape a training takes, and the one that answers the question a search result cannot:
/// <see cref="CatalogueTrainingDto"/> is <em>a way of finding a training rather than a way of
/// reading it</em>, and this is the reading. Separate for ADR 0055's reason — an audience is what
/// separates two contracts — even though this one's audience is the same as the search's.
/// <para>
/// It carries the trainer's <see cref="TrainerName"/> and not their identifier, which is the whole
/// difference between a page somebody can use and a page that shows a GUID. The name is read from
/// the write model at the moment of the request rather than kept in the search index: no
/// integration event carries a rename, so an indexed copy would be a name that nothing refreshes.
/// </para>
/// <para>
/// No photo, and that is a decision rather than an omission: a portrait served publicly would carry
/// whatever EXIF the phone that took it wrote, GPS included, which ADR 0021 names and defers. The
/// day that is stripped, this shape gains the reference and not before.
/// </para>
/// </remarks>
public sealed class CatalogueTrainingDetailDto
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
