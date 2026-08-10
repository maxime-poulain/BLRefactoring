namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// A row of <c>GET /Catalog/trainings</c>: a training anybody may be shown.
/// </summary>
/// <remarks>
/// Three members, and the shortest published contract here, because it answers the least specific
/// audience there is. It carries no status: a row a visitor reads is on offer, and the states that
/// are not — unpublished, withheld, or belonging to a suspended trainer — never reach this listing
/// (ADR 0050, ADR 0056, ADR 0059). It carries no reason for the same reason.
/// <para>
/// The trainer travels as an identifier and not as a name, unlike the administrative row. That is
/// not an oversight repeating itself: an administrator is deciding about a person and needs to read
/// who they are, while a visitor is looking at a catalog whose author pages do not exist yet.
/// Publishing every trainer's name to anybody is precisely the read this API withdrew.
/// </para>
/// </remarks>
public sealed class CatalogTrainingHttpResponse
{
    /// <summary>The training's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The trainer who offers it.</summary>
    public required Guid TrainerId { get; init; }

    /// <summary>The training's title.</summary>
    public required string Title { get; init; }
}
