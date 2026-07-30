using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.Shared.Application.Factories;

/// <summary>
/// Turns the primitives carried by an incoming request or command into the value
/// objects of the trainer aggregate, reporting every problem at once.
/// </summary>
/// <remarks>
/// Translating a message into domain concepts is an application-layer concern: the
/// domain exposes value objects and knows nothing about the shape of what the API
/// received. Both stacks and both use cases (creation and edition) go through here,
/// so the rules cannot drift between them.
/// </remarks>
public static class TrainerProfileFactory
{
    /// <summary>
    /// Builds the value objects making up a trainer's profile.
    /// </summary>
    /// <param name="bio">
    /// The raw bio. <see langword="null"/> means "no bio", which is valid — at
    /// creation none was provided, on edition the existing one is cleared.
    /// </param>
    public static Result<TrainerProfile> Create(
        string firstname,
        string lastname,
        string contactEmail,
        string? bio)
    {
        var errors = new ErrorCollection();

        Name? name = null;
        Email? email = null;
        Bio? trainerBio = null;

        Name.Create(firstname, lastname).Switch(value => name = value, errors.AddErrors);
        Email.Create(contactEmail).Switch(value => email = value, errors.AddErrors);

        if (bio is not null)
        {
            Bio.Create(bio).Switch(value => trainerBio = value, errors.AddErrors);
        }

        return errors.Any()
            ? Result<TrainerProfile>.Failure(errors)
            : Result<TrainerProfile>.Success(new TrainerProfile(name!, email!, trainerBio));
    }
}

/// <summary>
/// The validated value objects making up a trainer's profile, as handed from the
/// factory to the caller that will pass them on to the aggregate.
/// </summary>
/// <remarks>
/// An application-layer carrier, not a domain concept: the domain never references
/// it — <c>Trainer</c> takes the value objects themselves.
/// </remarks>
public sealed record TrainerProfile(Name Name, Email ContactEmail, Bio? Bio);
