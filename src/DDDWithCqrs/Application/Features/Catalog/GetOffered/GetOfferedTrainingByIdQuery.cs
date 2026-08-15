using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOffered;

/// <summary>
/// Asks for one offered training, by identifier, on behalf of nobody in particular (ADR 0062).
/// </summary>
/// <remarks>
/// The second read by identifier in this application, and the only one that names no caller.
/// <c>GetTrainingByIdQuery</c> is scoped to its owner inside the query itself — <em>a training
/// belonging to somebody else is not found</em> — which is what made keeping it safe. This one is
/// scoped by the search index instead: a training a visitor may see is one the index holds, and the
/// nine consumers of ADR 0056 decide that, not this message.
/// <para>
/// A <see langword="null"/> answer for both "no such training" and "not on offer", like its
/// neighbor, and the action turns it into a 404. Telling a visitor that a training exists but has
/// been taken down is the administration's read (ADR 0055).
/// </para>
/// </remarks>
public sealed class GetOfferedTrainingByIdQuery(Guid trainingId) : IQuery<CatalogTrainingDetailDto?>
{
    /// <summary>
    /// The training the visitor asked for.
    /// </summary>
    public Guid TrainingId { get; } = trainingId;
}
