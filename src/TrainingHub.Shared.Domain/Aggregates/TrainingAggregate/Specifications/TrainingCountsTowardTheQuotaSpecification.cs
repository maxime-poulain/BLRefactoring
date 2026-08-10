using System.Linq.Expressions;
using TrainingHub.Shared.Common.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// The training holds a place in its owner's catalog quota: everything the owner has not
/// withdrawn themselves.
/// </summary>
/// <remarks>
/// This criteria has changed meaning twice, and each time for a reason worth keeping.
/// <para>
/// It began as "every training the trainer owns", which was the same number until withdrawing became
/// possible. ADR 0050 narrowed it to "is published": a trainer who unpublishes all ten of theirs
/// offers the public nothing and must be able to write an eleventh, because the rule is about a
/// catalog on offer rather than about rows.
/// </para>
/// <para>
/// ADR 0052 widens it again, and the difference is <em>who withdrew it</em>. That argument was about
/// a <em>voluntary</em> withdrawal; freeing a slot by being moderated is a perverse incentive, and a
/// trainer whose training is withheld would otherwise be handed room for a replacement. So the
/// question is no longer "is it published" but "did its owner take it down" — which is why this type
/// replaced <c>TrainingIsPublishedSpecification</c> rather than quietly changing its expression: a
/// specification names a business rule, and that name had stopped being the rule.
/// </para>
/// <para>
/// One expression, answering in memory and as query criteria alike — the aggregate's rule and the
/// database's filter cannot drift apart because there is only one of them (ADR 0028).
/// </para>
/// </remarks>
public sealed class TrainingCountsTowardTheQuotaSpecification : Specification<Training>
{
    /// <summary>
    /// States the question.
    /// </summary>
    public TrainingCountsTowardTheQuotaSpecification()
        : base(Rule())
    {
    }

    // Stated as "not withdrawn" rather than as "published or withheld", so that a fourth state
    // added tomorrow counts by default. That is the safe direction: a state nobody thought about
    // occupying a slot refuses one creation too many, where the other reading would silently hand
    // out a slot the quota was meant to hold.
    private static Expression<Func<Training, bool>> Rule()
        => training => training.Status != TrainingStatus.Unpublished;
}
