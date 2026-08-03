using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Tests.Helpers;

/// <summary>
/// Builds a valid trainer. The aggregate only accepts value objects, so the builder
/// assembles them the same way the application layer does — an invalid input is a
/// value object concern and is covered by their own tests, not here.
/// </summary>
public sealed class TrainerBuilder
{
    private string _firstname = "John";
    private string _lastname = "Doe";
    private string _contactEmail = "john.doe@example.com";
    private string? _bio = "Experienced software trainer with 10 years of experience.";
    private Guid _userId = Guid.NewGuid();
    private Guid? _id;

    /// <summary>
    /// With firstname.
    /// </summary>
    public TrainerBuilder WithFirstname(string v) { _firstname = v; return this; }

    /// <summary>
    /// With lastname.
    /// </summary>
    public TrainerBuilder WithLastname(string v) { _lastname = v; return this; }

    /// <summary>
    /// With contact email.
    /// </summary>
    public TrainerBuilder WithContactEmail(string v) { _contactEmail = v; return this; }

    /// <summary>
    /// With bio.
    /// </summary>
    public TrainerBuilder WithBio(string v) { _bio = v; return this; }

    /// <summary>
    /// Without bio.
    /// </summary>
    public TrainerBuilder WithoutBio() { _bio = null; return this; }

    /// <summary>
    /// With user id.
    /// </summary>
    public TrainerBuilder WithUserId(Guid v) { _userId = v; return this; }

    /// <summary>
    /// With id.
    /// </summary>
    public TrainerBuilder WithId(Guid v) { _id = v; return this; }

    /// <summary>
    /// Build.
    /// </summary>
    public Trainer Build()
    {
        return Trainer.Create(
            _id.HasValue ? TrainerId.Create(_id.Value) : TrainerId.Generate(),
            UserId.Create(_userId),
            Name.Create(_firstname, _lastname).ShouldBeSuccess(),
            Email.Create(_contactEmail).ShouldBeSuccess(),
            _bio is null ? null : Bio.Create(_bio).ShouldBeSuccess());
    }
}
