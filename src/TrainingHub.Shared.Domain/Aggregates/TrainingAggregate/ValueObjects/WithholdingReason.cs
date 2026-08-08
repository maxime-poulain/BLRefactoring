using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Why a training was withheld, in the words of whoever withheld it.
/// </summary>
/// <remarks>
/// A value object rather than a <see langword="string"/>, and not for symmetry: the domain has rules
/// about it — present, not blank, bounded — and an aggregate here never accepts a raw string. It is
/// the shape <c>Bio</c> already has, down to the trim before the measure.
/// <para>
/// It lives on the aggregate rather than only on the fact that announced it, because the owner has
/// to be able to read it <em>afterwards</em>, and the outbox is not a store: ADR 0033 sweeps
/// delivered messages on a retention period, so a fact that has been delivered and swept cannot
/// answer "why is my training unavailable" (ADR 0052).
/// </para>
/// <para>
/// A type of its own rather than one shared with
/// <see cref="TrainerAggregate.ValueObjects.SuspensionReason"/>, because each aggregate owns the
/// error codes it raises (ADR 0015) and a shared value object would have no home for its refusals.
/// </para>
/// </remarks>
public sealed class WithholdingReason : ValueObject
{
    /// <summary>
    /// The longest a reason may be, sharing <c>Bio</c>'s bound for want of a reason to differ.
    /// </summary>
    public const int MaximumLength = 500;

    /// <summary>
    /// The text this value object wraps.
    /// </summary>
    public string Value { get; private init; } = null!;

    private WithholdingReason()
    {
    }

    /// <summary>
    /// Builds a <see cref="WithholdingReason"/> from raw input.
    /// </summary>
    /// <remarks>
    /// Blank is a refusal rather than an empty reason: a training taken out of public view without a
    /// word leaves its owner with nothing to act on, which is the whole thing this records.
    /// </remarks>
    /// <param name="value">What the administrator wrote.</param>
    /// <returns>The value, or the rule it broke.</returns>
    public static Result<WithholdingReason> Create(string value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result<WithholdingReason>.Failure(TrainingErrorCodes.WithholdingReasonEmpty,
                "Withholding a training must say why.");
        }

        if (trimmed.Length > MaximumLength)
        {
            return Result<WithholdingReason>.Failure(TrainingErrorCodes.WithholdingReasonTooLong,
                $"A withholding reason cannot exceed {MaximumLength} characters.");
        }

        return Result<WithholdingReason>.Success(new WithholdingReason { Value = trimmed });
    }

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
