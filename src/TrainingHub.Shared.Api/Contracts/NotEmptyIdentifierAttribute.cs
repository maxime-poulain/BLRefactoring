using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts;

/// <summary>
/// Refuses <see cref="Guid.Empty"/> on an identifier a caller supplies.
/// </summary>
/// <remarks>
/// <see cref="RequiredAttribute"/> cannot do this job, and this repository carried the proof:
/// <c>TransferTrainingHttpRequest.RecipientTrainerId</c> was marked <c>[Required]</c> and its own
/// remark claimed the attribute "only refuses a message with no recipient at all". It did not. A
/// non-nullable <see cref="Guid"/> always has a value, so <see cref="Guid.Empty"/> satisfies
/// <c>[Required]</c> and travels on — the annotation read as a guard and was none. What actually
/// refused it was a FluentValidation rule one layer down, on one host of the two (ADR 0046).
/// <para>
/// Absence stays <see cref="RequiredAttribute"/>'s business: a null value passes here, so the two
/// annotations compose on a nullable identifier rather than both answering for the same field.
/// </para>
/// <para>
/// Applied on a contract's property, <c>[ApiController]</c> answers a failure at model binding with
/// the <see cref="System.ComponentModel.DataAnnotations"/> path every other annotation on this
/// boundary already takes: a <c>ValidationProblemDetails</c> naming the field, before the action
/// runs and long before <c>EntityId.Create</c> is asked to build anything.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyIdentifierAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes the attribute with the message a caller reads.
    /// </summary>
    public NotEmptyIdentifierAttribute()
        : base("The {0} field must name an identifier, and the empty one names none.")
    {
    }

    /// <summary>
    /// Answers whether the value names an identifier.
    /// </summary>
    /// <param name="value">The bound value, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="false"/> only for <see cref="Guid.Empty"/>. A null passes, since absence is
    /// <see cref="RequiredAttribute"/>'s question; anything that is not a <see cref="Guid"/> passes
    /// too, which is the framework convention for an attribute asked about a type it does not judge.
    /// </returns>
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        Guid identifier => identifier != Guid.Empty,
        _ => true
    };
}
