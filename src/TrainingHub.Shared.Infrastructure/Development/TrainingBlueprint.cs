namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// One training as the development catalog writes it, before any trainer owns it.
/// </summary>
/// <remarks>
/// Every field is what a trainer would actually have typed, and the four of them are written to
/// agree with each other: a blueprint whose title promises production Kubernetes asks for Docker
/// and a pipeline in its prerequisites, never for nothing. That coherence is the whole point of
/// the corpus — a catalog of five hundred trainings whose prerequisites read <em>"none"</em> proves
/// the pagination works and teaches a reader nothing about the product.
/// <para>
/// Not validated here. Each string reaches the aggregate through the value object that owns its
/// rule, and a corpus entry that breaks one is a failing test rather than a silently truncated
/// row (<c>DevelopmentCatalogTests</c>).
/// </para>
/// </remarks>
/// <param name="Title">What the training is called.</param>
/// <param name="Description">What it covers.</param>
/// <param name="Prerequisites">What a participant needs beforehand.</param>
/// <param name="AcquiredSkills">What a participant leaves with.</param>
/// <param name="Topics">The subjects it is filed under, spelled as the domain spells them.</param>
public sealed record TrainingBlueprint(
    string Title,
    string Description,
    string Prerequisites,
    string AcquiredSkills,
    IReadOnlyList<string> Topics);
