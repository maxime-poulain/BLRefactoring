using TrainingHub.Shared.Api.Contracts.Administration;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Errors;
using TrainingHub.Shared.Api.Contracts.Outbox;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Errors;

namespace TrainingHub.Shared.Api.Contracts.Mappings;

/// <summary>
/// Translates what the application layer answers into what the API publishes.
/// </summary>
/// <remarks>
/// One translation for both hosts, for the same reason the projections of
/// <c>Shared.Application</c> are shared: two hosts advertised as serving the same REST API cannot
/// be allowed to describe the same trainer differently.
/// <para>
/// The direction of the dependency is the one the layering asks for — the API knows the
/// application layer, never the reverse. What changes is that the knowledge now stops here,
/// in a handful of methods, instead of running through every action signature and every
/// <c>[ProducesResponseType]</c>.
/// </para>
/// </remarks>
public static class ApplicationToHttpMappings
{
    /// <summary>
    /// Publishes a trainer read model. The row version is deliberately dropped: it leaves in the
    /// <c>ETag</c>, which the controller sets from the read model itself.
    /// </summary>
    /// <remarks>
    /// The verification flag arrives as a parameter rather than on the read model, because the
    /// fact is the Identity store's and the application layer deliberately does not know it: the
    /// controller reads it through <c>IAccountVerificationQuery</c> and stitches the two halves
    /// together here, at the boundary (ADR 0090).
    /// </remarks>
    public static TrainerHttpResponse ToHttp(this TrainerDto trainer, bool isEmailVerified)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new TrainerHttpResponse
        {
            Id = trainer.Id,
            Firstname = trainer.Firstname,
            Lastname = trainer.Lastname,
            ContactEmail = trainer.ContactEmail,
            Bio = trainer.Bio,
            PhotoId = trainer.PhotoId,
            Status = trainer.Status,
            IsEmailVerified = isEmailVerified,
            SuspensionReason = trainer.SuspensionReason
        };
    }

    /// <summary>
    /// Publishes a training read model.
    /// </summary>
    public static TrainingHttpResponse ToHttp(this TrainingDto training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return new TrainingHttpResponse
        {
            Id = training.Id,
            Title = training.Title,
            TrainerId = training.TrainerId,
            Topics = training.Topics,
            Description = training.Description,
            Prerequisites = training.Prerequisites,
            AcquiredSkills = training.AcquiredSkills,
            Status = training.Status,
            WithholdingReason = training.WithholdingReason
        };
    }

    /// <summary>
    /// Publishes a sequence of training read models.
    /// </summary>
    public static List<TrainingHttpResponse> ToHttp(this IEnumerable<TrainingDto> trainings)
        => [.. trainings.Select(ToHttp)];

    /// <summary>
    /// Publishes a trainer as the administration reads them.
    /// </summary>
    /// <remarks>
    /// A translation of its own rather than a widening of the one above, which is what ADR 0055's
    /// separate contracts amount to at this layer: the two shapes meet nowhere, so nothing added to
    /// one can reach the other.
    /// </remarks>
    public static AdministrationTrainerHttpResponse ToHttp(this AdministrationTrainerDto trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new AdministrationTrainerHttpResponse
        {
            Id = trainer.Id,
            Firstname = trainer.Firstname,
            Lastname = trainer.Lastname,
            ContactEmail = trainer.ContactEmail,
            Status = trainer.Status,
            SuspensionReason = trainer.SuspensionReason
        };
    }

    /// <summary>
    /// Publishes a page of them.
    /// </summary>
    public static List<AdministrationTrainerHttpResponse> ToHttp(
        this IEnumerable<AdministrationTrainerDto> trainers) => [.. trainers.Select(ToHttp)];

    /// <summary>
    /// Publishes a training as the administration reads it.
    /// </summary>
    public static AdministrationTrainingHttpResponse ToHttp(this AdministrationTrainingDto training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return new AdministrationTrainingHttpResponse
        {
            Id = training.Id,
            TrainerId = training.TrainerId,
            TrainerName = training.TrainerName,
            Title = training.Title,
            Status = training.Status,
            WithholdingReason = training.WithholdingReason
        };
    }

    /// <summary>
    /// Publishes a page of them.
    /// </summary>
    public static List<AdministrationTrainingHttpResponse> ToHttp(
        this IEnumerable<AdministrationTrainingDto> trainings) => [.. trainings.Select(ToHttp)];

    /// <summary>
    /// Publishes a training as the public catalog reads it.
    /// </summary>
    public static CatalogTrainingHttpResponse ToHttp(this CatalogTrainingDto training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return new CatalogTrainingHttpResponse
        {
            Id = training.Id,
            TrainerId = training.TrainerId,
            Title = training.Title
        };
    }

    /// <summary>
    /// Publishes a page of them.
    /// </summary>
    public static List<CatalogTrainingHttpResponse> ToHttp(
        this IEnumerable<CatalogTrainingDto> trainings) => [.. trainings.Select(ToHttp)];

    /// <summary>
    /// Publishes one facet of the offered catalog (ADR 0069).
    /// </summary>
    public static CatalogTopicHttpResponse ToHttp(this TopicFacetDto facet)
    {
        ArgumentNullException.ThrowIfNull(facet);

        return new CatalogTopicHttpResponse
        {
            Topic = facet.Topic,
            OfferedCount = facet.OfferedCount
        };
    }

    /// <summary>
    /// Publishes all of them, as the document <c>GET /Catalog/topics</c> answers.
    /// </summary>
    public static CatalogTopicsHttpResponse ToHttp(this IEnumerable<TopicFacetDto> facets) =>
        new() { Topics = [.. facets.Select(ToHttp)] };

    /// <summary>
    /// Publishes one offered training in full, for the visitor who followed a search result
    /// (ADR 0062).
    /// </summary>
    public static CatalogTrainingDetailHttpResponse ToHttp(this CatalogTrainingDetailDto training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return new CatalogTrainingDetailHttpResponse
        {
            Id = training.Id,
            Title = training.Title,
            TrainerName = training.TrainerName,
            TrainerId = training.TrainerId,
            Topics = training.Topics,
            Description = training.Description,
            Prerequisites = training.Prerequisites,
            AcquiredSkills = training.AcquiredSkills,
            TrainerPhotoId = training.TrainerPhotoId
        };
    }

    /// <summary>
    /// Publishes one offering trainer's public profile (ADR 0070).
    /// </summary>
    public static CatalogTrainerHttpResponse ToHttp(this CatalogTrainerDto trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new CatalogTrainerHttpResponse
        {
            Id = trainer.Id,
            Firstname = trainer.Firstname,
            Lastname = trainer.Lastname,
            Bio = trainer.Bio,
            PhotoId = trainer.PhotoId,
            Trainings = trainer.Trainings.ToHttp()
        };
    }

    /// <summary>
    /// Publishes one message the outbox gave up on (ADR 0061).
    /// </summary>
    public static PoisonedMessageHttpResponse ToHttp(this PoisonedMessageDto message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new PoisonedMessageHttpResponse
        {
            Id = message.Id,
            Name = message.Name,
            Version = message.Version,
            OccurredOnUtc = message.OccurredOnUtc,
            Attempts = message.Attempts,
            Error = message.Error,
            RequeuedOnUtc = message.RequeuedOnUtc,
            DeliveredConsumers = message.DeliveredConsumers
        };
    }

    /// <summary>
    /// Publishes a page of them.
    /// </summary>
    public static List<PoisonedMessageHttpResponse> ToHttp(
        this IEnumerable<PoisonedMessageDto> messages) => [.. messages.Select(ToHttp)];

    /// <summary>
    /// Publishes the errors an application call reported.
    /// </summary>
    /// <remarks>
    /// The shape matches what the kernel's <c>Error</c> used to serialize, nested code included,
    /// so no caller sees a difference. It is also the single place to change the day the API
    /// answers in <c>ProblemDetails</c>.
    /// </remarks>
    public static List<ErrorHttpResponse> ToHttp(this IEnumerable<Error> errors)
        => [.. errors.Select(error => new ErrorHttpResponse
        {
            ErrorMessage = error.ErrorMessage,
            ErrorCode = error.ErrorCode.Value
        })];
}
