using System.Linq.Expressions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainers;

/// <summary>
/// Projections from the <see cref="Trainer"/> aggregate to its read DTOs,
/// shared by the trainer query handlers.
/// </summary>
public static class TrainerProjections
{
    public static readonly Expression<Func<Trainer, TrainerDto>> ToDto = trainer => new TrainerDto
    {
        Id = trainer.Id.Value,
        ContactEmail = trainer.ContactEmail.FullAddress,
        Firstname = trainer.Name.Firstname,
        Lastname = trainer.Name.Lastname,
        Bio = trainer.Bio == null ? null : trainer.Bio.Value
    };
}
