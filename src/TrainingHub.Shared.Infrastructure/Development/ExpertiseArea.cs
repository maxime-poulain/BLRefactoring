namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// What one trainer of the development catalog knows about, and everything they could teach.
/// </summary>
/// <remarks>
/// The unit that makes the generated data coherent rather than merely plentiful. A trainer is
/// drawn from exactly one area, their biography is written for that area, and every training they
/// own comes from its blueprints — so nobody in the catalog teaches Kubernetes on Monday and brand
/// positioning on Tuesday. Shuffling trainings across areas would have been one line shorter and
/// would have produced a catalog no screenshot could be taken of.
/// <para>
/// The biographies are openings rather than whole texts: the dataset closes each with one line
/// from <see cref="DevelopmentCatalog.TeachingNotes"/>, so a dozen written sentences per area
/// yield a few hundred distinct biographies without any of them reading as generated.
/// </para>
/// </remarks>
/// <param name="Name">The area, as this corpus names it. Never shown to a visitor.</param>
/// <param name="Biographies">Opening sentences a trainer of this area could have written.</param>
/// <param name="Blueprints">Everything a trainer of this area could offer.</param>
public sealed record ExpertiseArea(
    string Name,
    IReadOnlyList<string> Biographies,
    IReadOnlyList<TrainingBlueprint> Blueprints);
