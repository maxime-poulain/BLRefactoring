using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;

namespace TrainingHub.DDD.Api.Mappings;

/// <summary>
/// Turns the API's request contracts into the messages this stack's application services accept.
/// </summary>
/// <remarks>
/// It lives in the API project because it is the only place that knows both sides. The contracts
/// are shared with the CQRS host; what they are translated into is not, which is exactly why the
/// translation is per stack while the contract is not.
/// </remarks>
public static class HttpToApplicationMappings
{
    /// <summary>
    /// Builds the edition request for <c>ITrainerApplicationService.EditAsync</c>. The trainer
    /// being edited and the version the caller read are passed alongside, not inside: they come
    /// from the token and from <c>If-Match</c>, not from the body.
    /// </summary>
    public static TrainerEditionRequest ToApplicationRequest(this EditTrainerHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TrainerEditionRequest
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = request.ContactEmail,
            Bio = request.Bio
        };
    }

    /// <summary>
    /// Builds the creation request the registration flow hands to
    /// <c>ITrainerApplicationService.CreateAsync</c>.
    /// </summary>
    /// <remarks>
    /// The contact address starts out as the account email; the trainer can make it diverge later
    /// from their profile.
    /// </remarks>
    /// <param name="request">The registration request.</param>
    /// <param name="userId">The identity user created moments earlier.</param>
    public static TrainerCreationRequest ToApplicationRequest(this RegisterHttpRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TrainerCreationRequest
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = request.Email,
            UserId = userId
        };
    }

    /// <summary>
    /// Builds the creation request for <c>ITrainingApplicationService.CreateAsync</c>.
    /// </summary>
    public static TrainingCreationRequest ToApplicationRequest(this CreateTrainingHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TrainingCreationRequest
        {
            Title = request.Title,
            Topics = request.Topics,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills
        };
    }

    /// <summary>
    /// Builds the edition request for <c>ITrainingApplicationService.EditAsync</c>.
    /// </summary>
    public static TrainingEditionRequest ToApplicationRequest(this EditTrainingHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TrainingEditionRequest
        {
            Title = request.Title,
            Topics = request.Topics,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills
        };
    }
}
