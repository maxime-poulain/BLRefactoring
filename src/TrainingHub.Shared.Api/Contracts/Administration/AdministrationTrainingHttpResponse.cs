namespace TrainingHub.Shared.Api.Contracts.Administration;

/// <summary>
/// A row of <c>GET /Administration/trainings</c>: a training, whose it is, and where it stands.
/// </summary>
/// <remarks>
/// Its own contract rather than <c>TrainingHttpResponse</c> with a field added (ADR 0055). It
/// carries five members where that one carries nine: moderating a training is decided on the
/// training itself, at its own address, not on a row of a list.
/// <para>
/// One consequence is worth stating rather than discovering. Because the withholding reason lives
/// here and not on <c>TrainingHttpResponse</c>, a trainer whose training was withheld still cannot
/// read why through their own listing — the consequence ADR 0052 named and this record leaves open.
/// It closes when the trainer's own surface learns to show it.
/// </para>
/// </remarks>
public sealed class AdministrationTrainingHttpResponse
{
    /// <summary>The training's identifier, which the administrative endpoints take.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer who owns it — the next question an administrator asks.</summary>
    public required Guid TrainerId { get; init; }

    /// <summary>The training's title.</summary>
    public required string Title { get; init; }

    /// <summary><c>Published</c>, <c>Unpublished</c> or <c>Withheld</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why the training was withheld, or <see langword="null"/> when it was not.
    /// </summary>
    /// <remarks>
    /// Present if and only if <see cref="Status"/> is <c>Withheld</c> — the invariant ADR 0052
    /// states on the aggregate, carried out to the boundary rather than restated as a rule here.
    /// </remarks>
    public string? WithholdingReason { get; init; }
}
