using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerProfile;

/// <summary>
/// Asks for one offering trainer's public profile, on behalf of nobody in particular (ADR 0070).
/// </summary>
/// <remarks>
/// The first read in this application that names a person to nobody in particular, and what makes
/// it safe is where the answer's visibility comes from: a profile answers if and only if the
/// search index holds at least one entry for this trainer, and the nine consumers of ADR 0056
/// decide that, not this message.
/// <para>
/// A <see langword="null"/> answer for a person nobody ever registered, a suspended one, and one
/// with nothing published alike, and the action turns all three into the same 404: telling a
/// visitor that a person exists but may not be looked at is the administration's read (ADR 0055).
/// </para>
/// </remarks>
public sealed class GetTrainerProfileQuery(Guid trainerId) : IQuery<CatalogTrainerDto?>
{
    /// <summary>
    /// The trainer the visitor asked for.
    /// </summary>
    public Guid TrainerId { get; } = trainerId;
}
