using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

namespace BLRefactoring.Shared.Domain.Tests.Helpers;

public class TrainerBuilder
{
    private string _firstname = "John";
    private string _lastname = "Doe";
    private string _email = "john.doe@example.com";
    private string _bio = "Experienced software trainer with 10 years of experience.";
    private Guid _userId = Guid.NewGuid();
    private Guid? _id;

    public TrainerBuilder WithFirstname(string v) { _firstname = v; return this; }
    public TrainerBuilder WithLastname(string v) { _lastname = v; return this; }
    public TrainerBuilder WithEmail(string v) { _email = v; return this; }
    public TrainerBuilder WithBio(string v) { _bio = v; return this; }
    public TrainerBuilder WithUserId(Guid v) { _userId = v; return this; }
    public TrainerBuilder WithId(Guid v) { _id = v; return this; }

    public Result<Trainer> Build()
    {
        if (_id.HasValue)
            return Trainer.Create(_id.Value, _firstname, _lastname, _email, _bio, _userId);

        return Trainer.Create(new TrainerCreationMessage
        {
            TrainerId = Guid.NewGuid(),
            Firstname = _firstname,
            Lastname = _lastname,
            Email = _email,
            Bio = _bio,
            UserId = _userId
        });
    }

    public Trainer BuildValid()
    {
        return Build().ShouldBeSuccess();
    }
}
