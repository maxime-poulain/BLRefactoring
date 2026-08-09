using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalogue.Search;

/// <summary>
/// One page of the offered catalogue, narrowed by a term when there is one (ADR 0059).
/// </summary>
/// <remarks>
/// The first query here that reads no aggregate table. Its neighbours ask the write model a
/// question and project columns out of it; this one asks the search index, which is a read model
/// maintained after the commit by nine consumers of the outbox (ADR 0025, ADR 0056).
/// <para>
/// It carries a term where <c>GetAdministeredTrainingsQuery</c> carries none, and the asymmetry has
/// swapped sides rather than disappeared: a title is value-converted in the write model and
/// searchable in the index, so the search this record adds is the public one — a moderator still
/// needs the states the index refuses to hold.
/// </para>
/// <para>
/// No status, and nothing that names a caller. What may be shown is composed when the index is
/// written rather than asked for when it is read, which is what makes an anonymous read of it safe.
/// </para>
/// </remarks>
public sealed class SearchCatalogueQuery : IQuery<PagedResult<CatalogueTrainingDto>>
{
    /// <summary>
    /// What to look for, or <see langword="null"/> for the whole offered catalogue.
    /// </summary>
    public string? Term { get; init; }

    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
