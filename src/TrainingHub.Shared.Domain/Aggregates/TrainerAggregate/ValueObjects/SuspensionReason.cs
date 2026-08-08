using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Why a trainer was suspended, in the words of whoever suspended them.
/// </summary>
/// <remarks>
/// A value object rather than a <see langword="string"/>, and not for symmetry: the domain has rules
/// about it — present, not blank, bounded — and an aggregate here never accepts a raw string. It is
/// the shape <see cref="Bio"/> already has, down to the trim before the measure.
/// <para>
/// It is written by <see cref="Trainer.Suspend"/> and cleared by <see cref="Trainer.Reinstate"/>,
/// which are the only two writers there are. The invariant is stated in both directions — the reason
/// is present <em>if and only if</em> the trainer is suspended — and that is what forbids an orphan
/// reason and a mute sanction alike (ADR 0052).
/// </para>
/// <para>
/// A type of its own rather than one shared with
/// <see cref="TrainingAggregate.ValueObjects.WithholdingReason"/>, because each aggregate owns the
/// error codes it raises (ADR 0015) and a shared value object would have no home for its refusals.
/// </para>
/// </remarks>
public sealed class SuspensionReason : ValueObject
{
    /// <summary>
    /// The longest a reason may be, sharing <see cref="Bio"/>'s bound for want of a reason to differ.
    /// </summary>
    public const int MaximumLength = 500;

    /// <summary>
    /// The text this value object wraps.
    /// </summary>
    public string Value { get; private init; } = null!;

    private SuspensionReason()
    {
    }

    /// <summary>
    /// Builds a <see cref="SuspensionReason"/> from raw input.
    /// </summary>
    /// <remarks>
    /// Blank is a refusal rather than an empty reason: a sanction the product cannot explain to the
    /// person it lands on is the defect this value object exists to prevent.
    /// </remarks>
    /// <param name="value">What the administrator wrote.</param>
    /// <returns>The value, or the rule it broke.</returns>
    public static Result<SuspensionReason> Create(string value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result<SuspensionReason>.Failure(TrainerErrorCodes.SuspensionReasonEmpty,
                "A suspension must say why.");
        }

        if (trimmed.Length > MaximumLength)
        {
            return Result<SuspensionReason>.Failure(TrainerErrorCodes.SuspensionReasonTooLong,
                $"A suspension reason cannot exceed {MaximumLength} characters.");
        }

        return Result<SuspensionReason>.Success(new SuspensionReason { Value = trimmed });
    }

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
