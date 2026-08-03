namespace TrainingHub.Architecture.Tests.Framework;

/// <summary>
/// Names the record — or the README section — a rule defends, and the sentence it defends.
/// </summary>
/// <remarks>
/// Read twice by reflection over this assembly: by the coverage rules, which fail when a record is
/// defended by nothing, and by <see cref="RuleAssertions"/>, which turns a violation into a failure
/// quoting the decision rather than a boolean. Both readers mean the number and the sentence are
/// each written exactly once, at the rule that enforces them.
/// <para>
/// Repeatable, because one rule can answer to two records: the error-shape rules defend 0004 and
/// 0012, which amends it.
/// </para>
/// <para>
/// A rule with no attribute is invisible to the coverage test, which would let a record look
/// defended by a rule that never names it. <c>SuiteSelfRules</c> closes that.
/// </para>
/// </remarks>
/// <param name="record">
/// A record number — <c>"0012"</c> — or a README anchor — <c>"README#the-dependency-rule"</c>.
/// </param>
/// <param name="rule">
/// What the record says, in its own words, short enough to read inside a failure message.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ArchitectureRuleAttribute(string record, string rule) : Attribute
{
    /// <summary>
    /// Record.
    /// </summary>
    public string Record { get; } = record;

    /// <summary>
    /// Rule.
    /// </summary>
    public string Rule { get; } = rule;
}
