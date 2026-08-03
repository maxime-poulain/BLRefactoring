using TrainingHub.Shared.Api.Contracts.Errors;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
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
    public static TrainerResponseHttp ToHttp(this TrainerDto trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        return new TrainerResponseHttp
        {
            Id = trainer.Id,
            Firstname = trainer.Firstname,
            Lastname = trainer.Lastname,
            ContactEmail = trainer.ContactEmail,
            Bio = trainer.Bio,
            PhotoId = trainer.PhotoId
        };
    }

    /// <summary>
    /// Publishes a sequence of trainer read models.
    /// </summary>
    public static List<TrainerResponseHttp> ToHttp(this IEnumerable<TrainerDto> trainers)
        => [.. trainers.Select(ToHttp)];

    /// <summary>
    /// Publishes a training read model.
    /// </summary>
    public static TrainingResponseHttp ToHttp(this TrainingDto training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return new TrainingResponseHttp
        {
            Id = training.Id,
            Title = training.Title,
            TrainerId = training.TrainerId,
            Topics = training.Topics,
            Description = training.Description,
            Prerequisites = training.Prerequisites,
            AcquiredSkills = training.AcquiredSkills
        };
    }

    /// <summary>
    /// Publishes a sequence of training read models.
    /// </summary>
    public static List<TrainingResponseHttp> ToHttp(this IEnumerable<TrainingDto> trainings)
        => [.. trainings.Select(ToHttp)];

    /// <summary>
    /// Publishes the errors an application call reported.
    /// </summary>
    /// <remarks>
    /// The shape matches what the kernel's <c>Error</c> used to serialise, nested code included,
    /// so no caller sees a difference. It is also the single place to change the day the API
    /// answers in <c>ProblemDetails</c>.
    /// </remarks>
    public static List<ErrorResponseHttp> ToHttp(this IEnumerable<Error> errors)
        => [.. errors.Select(error => new ErrorResponseHttp
        {
            ErrorMessage = error.ErrorMessage,
            ErrorCode = error.ErrorCode.Value
        })];
}
