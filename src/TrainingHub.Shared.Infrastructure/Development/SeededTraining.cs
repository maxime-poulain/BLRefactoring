namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// One training the dataset asks the seeder to create, with the owner already decided.
/// </summary>
/// <remarks>
/// A blueprint that has been placed: it belongs to somebody, it appeared at a moment, and it is in
/// one of the three states a training can be in. The seeder reads it and calls the domain; it
/// invents nothing of its own (ADR 0079).
/// <para>
/// The state is carried as two fields rather than one closed enumeration, and the pair is
/// exclusive by construction — the dataset never sets both, and <c>DevelopmentDatasetTests</c>
/// holds that. A third representation of the domain's three states, declared beside the two it
/// already has, would be a set to keep in step for no benefit: this record is read by exactly one
/// caller, which turns it straight into a call to <c>Unpublish</c> or <c>Withhold</c>.
/// </para>
/// </remarks>
/// <param name="Title">What the training is called.</param>
/// <param name="Description">What it covers.</param>
/// <param name="Prerequisites">What a participant needs beforehand.</param>
/// <param name="AcquiredSkills">What a participant leaves with.</param>
/// <param name="Topics">The subjects it is filed under, spelled as the domain spells them.</param>
/// <param name="CreatedOnUtc">When it appeared, spread over the dataset's whole window.</param>
/// <param name="IsWithdrawnByOwner">Whether its owner took it off the catalog.</param>
/// <param name="WithholdingReason">Why the administration kept it back, or <see langword="null"/>.</param>
public sealed record SeededTraining(
    string Title,
    string Description,
    string Prerequisites,
    string AcquiredSkills,
    IReadOnlyList<string> Topics,
    DateTime CreatedOnUtc,
    bool IsWithdrawnByOwner,
    string? WithholdingReason)
{
    /// <summary>
    /// Whether this training is left on offer, which most of them are.
    /// </summary>
    public bool IsPublished => !IsWithdrawnByOwner && WithholdingReason is null;
}
