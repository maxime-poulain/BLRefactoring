namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Mailing;

public interface ITrainerCreationEmailService
{
    /// <summary>
    /// Notifies a trainer that he has been created.
    /// </summary>
    /// <param name="trainerId">The trainer's id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task NotifyAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default);
}

public sealed class TrainerCreationEmailService : ITrainerCreationEmailService
{
    // Contains the logic for building the HTML of the email and notify another service
    // such `Emailing` to proceed to the actual sending of the email.
    public Task NotifyAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
