using System.Linq.Expressions;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Application.Projections;

/// <summary>
/// The single description of how a <see cref="Trainer"/> becomes a <see cref="TrainerDto"/>.
/// </summary>
/// <remarks>
/// Same arrangement as <see cref="TrainingProjections"/>: the expression is what EF Core
/// translates, and the compiled delegate is what the layered stack calls on an aggregate it has
/// already loaded.
/// </remarks>
public static class TrainerProjections
{
    /// <summary>
    /// The mapping itself, in the form EF Core can translate.
    /// </summary>
    /// <remarks>
    /// The bio is read through a ternary rather than <c>trainer.Bio?.Value</c>: null-conditional
    /// access is not allowed in an expression tree.
    /// </remarks>
    public static readonly Expression<Func<Trainer, TrainerDto>> ToDtoExpression = trainer => new TrainerDto
    {
        RowVersion = trainer.RowVersion,
        Id = trainer.Id.Value,
        ContactEmail = trainer.ContactEmail.FullAddress,
        Firstname = trainer.Name.Firstname,
        Lastname = trainer.Name.Lastname,
        Bio = trainer.Bio == null ? null : trainer.Bio.Value
    };

    private static readonly Func<Trainer, TrainerDto> Compiled = ToDtoExpression.Compile();

    /// <summary>
    /// Maps an aggregate already loaded in memory.
    /// </summary>
    public static TrainerDto ToDto(this Trainer trainer) => Compiled(trainer);
}
