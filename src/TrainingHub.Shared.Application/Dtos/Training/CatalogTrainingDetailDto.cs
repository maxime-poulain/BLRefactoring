namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// One offered training, as a visitor reads it (ADR 0062).
/// </summary>
/// <remarks>
/// The fourth shape a training takes, and the one that answers the question a search result cannot:
/// <see cref="CatalogTrainingDto"/> is <em>a way of finding a training rather than a way of
/// reading it</em>, and this is the reading. Separate for ADR 0055's reason — an audience is what
/// separates two contracts — even though this one's audience is the same as the search's.
/// <para>
/// It carries the trainer twice, and each half does one job: the <see cref="TrainerName"/> is what
/// the page prints — read from the write model at the moment of the request rather than kept in
/// the search index, because no integration event carries a rename — and the
/// <see cref="TrainerId"/> is where the name links, now that the person has a public page of their
/// own to link to (ADR 0070).
/// </para>
/// <para>
/// The portrait travels as the identity of a <em>photo</em>, which is what let one be published at
/// all. The reference waited for the stripping ADR 0021 deferred: a portrait served publicly
/// carries whatever the phone that took it wrote, GPS included, so until something removed that
/// there was nothing safe to point at. ADR 0063 removed it, and a portrait that carries no proof
/// of having been stripped leaves this null.
/// </para>
/// </remarks>
public sealed class CatalogTrainingDetailDto
{
    /// <summary>The training's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The training's title.</summary>
    public required string Title { get; init; }

    /// <summary>The trainer who offers it, named for printing beside the title.</summary>
    public required string TrainerName { get; init; }

    /// <summary>The trainer who offers it, identified so the name can link to their page.</summary>
    public required Guid TrainerId { get; init; }

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
    /// Null for two different situations, and the reading is deliberately the same: the trainer has
    /// no photo, or they have one stored before anything stripped it (ADR 0063). Either way the
    /// page shows a name and no face.
    /// </remarks>
    public Guid? TrainerPhotoId { get; init; }
}
