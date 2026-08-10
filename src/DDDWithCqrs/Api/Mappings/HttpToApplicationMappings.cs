using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOffered;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOfferedPortrait;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.Search;
using TrainingHub.DDDWithCqrs.Application.Features.Outbox.GetPoisoned;
using TrainingHub.DDDWithCqrs.Application.Features.Outbox.Requeue;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Edit;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetAdministered;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Reinstate;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Suspend;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Create;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Delete;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Edit;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetAdministered;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetById;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetMine;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Publish;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Release;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Transfer;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Unpublish;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Withhold;
using TrainingHub.Shared.Api.Contracts.Administration;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;

namespace TrainingHub.DDDWithCqrs.Api.Mappings;

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
    public static CreateTrainerCommand ToCommand(this RegisterHttpRequest request, Guid userId)
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
        this EditTrainerHttpRequest request,
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
    public static CreateTrainingCommand ToCommand(this CreateTrainingHttpRequest request)
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
        this EditTrainingHttpRequest request,
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

    /// <summary>Builds the command handing a training over to another trainer (ADR 0036).</summary>
    public static TransferTrainingCommand ToCommand(this TransferTrainingHttpRequest request, Guid trainingId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TransferTrainingCommand(trainingId, request.RecipientTrainerId);
    }

    /// <summary>Builds the command offering a withdrawn training to the public again (ADR 0050).</summary>
    public static PublishTrainingCommand ToPublishTrainingCommand(Guid trainingId) => new(trainingId);

    /// <summary>Builds the command withdrawing a training from public view (ADR 0050).</summary>
    public static UnpublishTrainingCommand ToUnpublishTrainingCommand(Guid trainingId) => new(trainingId);

    /// <summary>Builds the command placing a trainer under sanction (ADR 0052).</summary>
    public static SuspendTrainerCommand ToCommand(this SuspendTrainerHttpRequest request, Guid trainerId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SuspendTrainerCommand(trainerId, request.Reason);
    }

    /// <summary>Builds the command lifting a trainer's sanction (ADR 0050).</summary>
    public static ReinstateTrainerCommand ToReinstateTrainerCommand(Guid trainerId) => new(trainerId);

    /// <summary>Builds the command taking a training out of public view (ADR 0052).</summary>
    public static WithholdTrainingCommand ToCommand(this WithholdTrainingHttpRequest request, Guid trainingId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new WithholdTrainingCommand(trainingId, request.Reason);
    }

    /// <summary>Builds the command lifting the interdiction on a withheld training (ADR 0052).</summary>
    public static ReleaseTrainingCommand ToReleaseTrainingCommand(Guid trainingId) => new(trainingId);

    /// <summary>Builds the query reading one page of trainers for the administration (ADR 0055).</summary>
    /// <remarks>
    /// Two bound objects become one message. The filter and the paging arrive as separate
    /// <c>[FromQuery]</c> contracts, because the page coordinates are the same everywhere and the
    /// criteria are this endpoint's own; joining them is the mapping's job, exactly as it is for a
    /// route identifier and a body.
    /// </remarks>
    public static GetAdministeredTrainersQuery ToQuery(
        this AdministrationTrainerFilterHttpRequest? filter,
        PaginationHttpRequest? pagination) => new()
        {
            Status = filter?.Status,
            Search = filter?.Search,
            Paging = pagination.ToPageRequest()
        };

    /// <summary>Builds the query reading one page of trainings for the administration (ADR 0055).</summary>
    public static GetAdministeredTrainingsQuery ToQuery(
        this AdministrationTrainingFilterHttpRequest? filter,
        PaginationHttpRequest? pagination) => new()
        {
            Status = filter?.Status,
            Search = filter?.Search,
            Paging = pagination.ToPageRequest()
        };

    /// <summary>Builds the query searching the public catalog (ADR 0059).</summary>
    /// <remarks>
    /// The same two-bound-objects-into-one-message shape as the administrative listings above, for
    /// the endpoint that has no status to carry: the index holds only what may be shown.
    /// </remarks>
    public static SearchCatalogQuery ToQuery(
        this CatalogSearchHttpRequest? search,
        PaginationHttpRequest? pagination) => new()
        {
            Term = search?.Term,
            Paging = pagination.ToPageRequest()
        };

    /// <summary>Builds the query reading one offered training in full (ADR 0062).</summary>
    /// <remarks>
    /// No bound object at all: the only thing this endpoint carries is the identifier the route
    /// names, and a visitor has nothing else to say.
    /// </remarks>
    public static GetOfferedTrainingQuery ToGetOfferedTrainingQuery(Guid trainingId) => new(trainingId);

    /// <summary>Builds the query reading the portrait behind an offered training (ADR 0063).</summary>
    /// <remarks>
    /// Both identifiers come from the route and neither names a person: the training is what the
    /// visitor followed, and the photo is what makes the answer cacheable forever.
    /// </remarks>
    public static GetOfferedPortraitQuery ToGetOfferedPortraitQuery(Guid trainingId, Guid photoId) =>
        new(trainingId, photoId);

    /// <summary>Builds the query reading one page of the outbox's poison (ADR 0061).</summary>
    /// <remarks>
    /// One bound object rather than two, because this listing has no criteria of its own: what is
    /// poison is settled by the configured retry budget, not by anything a caller may ask for.
    /// </remarks>
    public static GetPoisonedMessagesQuery ToPoisonedMessagesQuery(PaginationHttpRequest? pagination) => new()
    {
        Paging = pagination.ToPageRequest()
    };

    /// <summary>Builds the command handing one poison message back to the worker (ADR 0061).</summary>
    public static RequeueOutboxMessageCommand ToRequeueOutboxMessageCommand(Guid messageId) => new(messageId);

    /// <summary>Builds the query reading one trainer.</summary>
    public static GetTrainerByIdQuery ToGetTrainerByIdQuery(Guid trainerId) => new(trainerId);

    /// <summary>Builds the query reading one training.</summary>
    public static GetTrainingByIdQuery ToGetTrainingByIdQuery(Guid trainingId) => new(trainingId);

    /// <summary>Builds the query reading one page of the calling trainer's own trainings.</summary>
    /// <remarks>
    /// Paging and nothing else. Whose trainings these are is not the boundary's business: the
    /// handler resolves it from the authenticated caller, so this mapping has no identity to carry
    /// and no way to carry the wrong one. The paging translation itself is the shared
    /// <c>ToPageRequest</c>, so this host and the layered one cannot disagree on a default.
    /// </remarks>
    public static GetMyTrainingsQuery ToGetMyTrainingsQuery(this PaginationHttpRequest pagination)
        => new() { Paging = pagination.ToPageRequest() };
}
