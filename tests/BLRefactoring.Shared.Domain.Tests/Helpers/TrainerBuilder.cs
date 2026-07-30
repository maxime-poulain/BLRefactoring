using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Tests.Helpers;

/// <summary>
/// Builds a valid trainer. The aggregate only accepts value objects, so the builder
/// assembles them the same way the application layer does — an invalid input is a
/// value object concern and is covered by their own tests, not here.
/// </summary>
public class TrainerBuilder
{
    private string _firstname = "John";
    private string _lastname = "Doe";
    private string _contactEmail = "john.doe@example.com";
    private string? _bio = "Experienced software trainer with 10 years of experience.";
    private Guid _userId = Guid.NewGuid();
    private Guid? _id;

    public TrainerBuilder WithFirstname(string v) { _firstname = v; return this; }
    public TrainerBuilder WithLastname(string v) { _lastname = v; return this; }
    public TrainerBuilder WithContactEmail(string v) { _contactEmail = v; return this; }
    public TrainerBuilder WithBio(string v) { _bio = v; return this; }
    public TrainerBuilder WithoutBio() { _bio = null; return this; }
    public TrainerBuilder WithUserId(Guid v) { _userId = v; return this; }
    public TrainerBuilder WithId(Guid v) { _id = v; return this; }

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
