using System.Linq.Expressions;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Application.Projections;

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
        Bio = trainer.Bio == null ? null : trainer.Bio.Value,
        PhotoId = trainer.Photo == null ? null : trainer.Photo.PhotoId.Value
    };

    private static readonly Func<Trainer, TrainerDto> Compiled = ToDtoExpression.Compile();

    /// <summary>
    /// Maps an aggregate already loaded in memory.
    /// </summary>
    public static TrainerDto ToDto(this Trainer trainer) => Compiled(trainer);

    /// <summary>
    /// The administration's mapping, in the form EF Core can translate.
    /// </summary>
    /// <remarks>
    /// A second expression rather than a superset of the first, which is what ADR 0055's choice of
    /// separate contracts costs and buys: the two are read by different audiences, so they are kept
    /// apart here as well, where the columns are chosen. Both are declared beside each other on
    /// purpose — a reader comparing them sees in one screen what the administration is shown and
    /// what a trainer is, which no amount of prose about "the admin view" would convey.
    /// <para>
    /// The reason is read through a ternary for the same reason the bio is: null-conditional access
    /// is not allowed in an expression tree.
    /// </para>
    /// </remarks>
    public static readonly Expression<Func<Trainer, AdministrationTrainerDto>> ToAdministrationDtoExpression =
        trainer => new AdministrationTrainerDto
        {
            Id = trainer.Id.Value,
            Firstname = trainer.Name.Firstname,
            Lastname = trainer.Name.Lastname,
            ContactEmail = trainer.ContactEmail.FullAddress,
            Status = trainer.Status.Name,
            SuspensionReason = trainer.SuspensionReason == null ? null : trainer.SuspensionReason.Value
        };

    private static readonly Func<Trainer, AdministrationTrainerDto> CompiledAdministration =
        ToAdministrationDtoExpression.Compile();

    /// <summary>
    /// Maps an aggregate already loaded in memory, for the administration.
    /// </summary>
    public static AdministrationTrainerDto ToAdministrationDto(this Trainer trainer) =>
        CompiledAdministration(trainer);
}
