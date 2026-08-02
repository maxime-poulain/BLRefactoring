using BLRefactoring.DDDWithCqrs.Api.Contracts;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetMine;
using BLRefactoring.Shared.Api.Contracts.Trainers;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using BLRefactoring.DDDWithCqrs.Application.Pagination;

namespace BLRefactoring.DDDWithCqrs.Api.Mappings;

/// <summary>
/// Turns the API's request contracts into this stack's commands and queries.
/// </summary>
/// <remarks>
/// Every command a controller used to receive from model binding is now built here, from a
/// request contract plus whatever the route, the token and the headers supply. That is what
/// removes the two habits this refactoring set out to end: an application message deserialised
/// straight off the wire, and a controller assigning fields to it afterwards.
/// <para>
/// The queries taking nothing but a route value are mapped here too. The indirection is thin, but
/// it is what keeps <c>Application.Features</c> out of the controllers entirely — a boundary that
/// holds only where nothing crosses it.
/// </para>
/// </remarks>
public static class HttpToApplicationMappings
{
    /// <summary>
    /// Builds the command the registration flow dispatches to create the trainer.
    /// </summary>
    /// <remarks>
    /// The contact address starts out as the account email; the trainer can make it diverge later
    /// from their profile.
    /// </remarks>
    /// <param name="request">The registration request.</param>
    /// <param name="userId">The identity user created moments earlier.</param>
    public static CreateTrainerCommand ToCommand(this RegisterRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateTrainerCommand
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = request.Email,
            UserId = userId
        };
    }

    /// <summary>
    /// Builds the command replacing a trainer's profile.
    /// </summary>
    /// <param name="request">What the caller sent in the body.</param>
    /// <param name="expectedVersion">The version read from the <c>If-Match</c> header.</param>
    public static EditTrainerCommand ToCommand(
        this EditTrainerRequestHttp request,
        byte[] expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new EditTrainerCommand
        {
            ExpectedVersion = expectedVersion,
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = request.ContactEmail,
            Bio = request.Bio
        };
    }

    /// <summary>
    /// Builds the command creating a training. The identifier the command generates is the one the
    /// controller publishes in <c>Location</c>.
    /// </summary>
    public static CreateTrainingCommand ToCommand(this CreateTrainingRequestHttp request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateTrainingCommand
        {
            Title = request.Title,
            Topics = request.Topics,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills
        };
    }

    /// <summary>
    /// Builds the command replacing a training.
    /// </summary>
    /// <param name="request">What the caller sent in the body.</param>
    /// <param name="trainingId">The training named in the route.</param>
    /// <param name="expectedVersion">The version read from the <c>If-Match</c> header.</param>
    public static EditTrainingCommand ToCommand(
        this EditTrainingRequestHttp request,
        Guid trainingId,
        byte[] expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new EditTrainingCommand
        {
            TrainingId = trainingId,
            ExpectedVersion = expectedVersion,
            Title = request.Title,
            Topics = request.Topics,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills
        };
    }

    /// <summary>Builds the command deleting a training.</summary>
    public static DeleteTrainingCommand ToDeleteTrainingCommand(Guid trainingId) => new(trainingId);

    /// <summary>Builds the query reading one trainer.</summary>
    public static GetTrainerByIdQuery ToGetTrainerByIdQuery(Guid trainerId) => new(trainerId);

    /// <summary>Builds the query reading one training.</summary>
    public static GetTrainingByIdQuery ToGetTrainingByIdQuery(Guid trainingId) => new(trainingId);

    /// <summary>Builds the query reading one page of the calling trainer's own trainings.</summary>
    /// <remarks>
    /// Paging and nothing else. Whose trainings these are is not the boundary's business: the
    /// handler resolves it from the authenticated caller, so this mapping has no identity to carry
    /// and no way to carry the wrong one.
    /// </remarks>
    public static GetMyTrainingsQuery ToGetMyTrainingsQuery(this PaginationRequestHttp pagination)
        => new() { Page = Page(pagination), PageSize = PageSize(pagination) };

    // An absent [FromQuery] contract binds to null when the caller passes no parameter at all,
    // so the defaults live here as well as on the contract.
    private static int Page(PaginationRequestHttp? pagination) => pagination?.Page ?? 1;

    private static int PageSize(PaginationRequestHttp? pagination)
        => pagination?.PageSize ?? PagedQuery.DefaultPageSize;

    /// <summary>
    /// Publishes one page of read models, recomputing the metadata from the query result.
    /// </summary>
    public static PagedResponseHttp<TResponse> ToHttp<TItem, TResponse>(
        this PagedResult<TItem> page,
        Func<IEnumerable<TItem>, List<TResponse>> mapItems)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(mapItems);

        return new PagedResponseHttp<TResponse>
        {
            Items = mapItems(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
            HasNextPage = page.HasNextPage,
            HasPreviousPage = page.HasPreviousPage
        };
    }
}
