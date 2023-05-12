using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;

public class Trainer : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Email { get; private set; }

    private Trainer() { } // Private constructor for ORM or serialization

    public Trainer(string name, string email)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
    }
}
