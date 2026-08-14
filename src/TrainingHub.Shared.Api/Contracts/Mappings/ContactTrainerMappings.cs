using TrainingHub.Shared.Api.Contracts.Catalog;

namespace TrainingHub.Shared.Api.Contracts.Mappings;

/// <summary>
/// What both stacks read the same way out of a contact request (ADR 0082).
/// </summary>
/// <remarks>
/// One question, asked once. Whether a request looks automated is a boundary judgment, and having
/// each host answer it in its own mapping is how the two would eventually disagree about which
/// visitors get through — the defect ADR 0049's detector exists to see, on a field where the two
/// halves failing differently would be silent.
/// </remarks>
public static class ContactTrainerMappings
{
    /// <summary>
    /// Whether the honeypot came back filled.
    /// </summary>
    /// <remarks>
    /// Whitespace counts as empty: a browser that trims, an autofill that writes a space and a
    /// person who never saw the field are all the same caller, and refusing them would be refusing
    /// a human for a field they cannot see.
    /// </remarks>
    /// <param name="request">The contact request as it was bound.</param>
    /// <returns><see langword="true"/> when the invisible field carries something.</returns>
    public static bool LooksAutomated(this ContactTrainerHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return !string.IsNullOrWhiteSpace(request.Website);
    }
}
