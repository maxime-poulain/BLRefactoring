using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared;

/// <summary>
/// Queues integration events for publication after the current transaction commits.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Enqueue"/> stages the message alongside the business changes so both are written
/// by the same <c>SaveChanges</c>. Either the fact and its announcement are both persisted, or
/// neither is — which is the entire point: sending an email from a domain event handler runs
/// before the commit, so a failing transaction leaves an email already gone for a trainer that
/// never existed.
/// </para>
/// <para>
/// Nothing is sent here. Publication happens later, from the background processor, once the
/// transaction the message belongs to has actually committed.
/// </para>
/// </remarks>
public interface IOutbox
{
    /// <summary>
    /// Stages <paramref name="integrationEvent"/> for publication, in the ambient unit of work.
    /// </summary>
    void Enqueue(IIntegrationEvent integrationEvent);
}
