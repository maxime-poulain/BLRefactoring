using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.Contact;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// The second guard on the identifiers a contact message carries.
/// </summary>
/// <remarks>
/// The route constraint refuses a trainer identifier that is not a GUID, and this refuses the one
/// GUID that names nothing, for whatever reaches <c>ICommandDispatcher</c> by another road
/// (ADR 0046). The training's identifier is optional by design — a visitor writing from a
/// trainer's own page names none — so it is judged only when present.
/// </remarks>
public sealed class ContactTrainerCommandValidatorTests
{
    /// <summary>
    /// Validate, a message naming its trainer, accepts it.
    /// </summary>
    [Fact]
    public void Validate_AMessageNamingItsTrainer_AcceptsIt() =>
        new ContactTrainerCommandValidator()
            .Validate(Contact(Guid.CreateVersion7(), trainingId: null))
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, a training named as well, accepts it.
    /// </summary>
    [Fact]
    public void Validate_ATrainingNamedAsWell_AcceptsIt() =>
        new ContactTrainerCommandValidator()
            .Validate(Contact(Guid.CreateVersion7(), Guid.CreateVersion7()))
            .IsValid.Should().BeTrue();

    /// <summary>
    /// Validate, the empty trainer identifier, refuses it by naming the field.
    /// </summary>
    [Fact]
    public void Validate_TheEmptyTrainerIdentifier_RefusesItByNamingTheField()
    {
        var result = new ContactTrainerCommandValidator()
            .Validate(Contact(Guid.Empty, trainingId: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(ContactTrainerCommand.TrainerId));
    }

    /// <summary>
    /// Validate, the empty training identifier, refuses what its absence would have passed.
    /// </summary>
    /// <remarks>
    /// The two shapes of "no training" are deliberately different: absent means the visitor wrote
    /// from the trainer's page, and empty means a caller built the message wrong. Refusing only
    /// the second is what lets <c>NotEmptyIdentifier</c> and the optionality compose rather than
    /// answer for the same thing (ADR 0046).
    /// </remarks>
    [Fact]
    public void Validate_TheEmptyTrainingIdentifier_RefusesWhatItsAbsenceWouldHavePassed()
    {
        var result = new ContactTrainerCommandValidator()
            .Validate(Contact(Guid.CreateVersion7(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(ContactTrainerCommand.TrainingId));
    }

    private static ContactTrainerCommand Contact(Guid trainerId, Guid? trainingId) => new()
    {
        TrainerId = trainerId,
        TrainingId = trainingId,
        SenderFirstname = "Grace",
        SenderLastname = "Hopper",
        SenderEmailAddress = "grace@example.org",
        Message = "I would like to book this training."
    };
}
