using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts;

/// <summary>
/// Refuses a sort name the catalog does not publish, on a filter a caller supplies.
/// </summary>
/// <remarks>
/// <see cref="KnownTopicAttribute"/>'s sibling, with the enum standing where the value object
/// stands there: the allowed values are read off <c>CatalogOrder</c>'s members rather than
/// restated, so publishing an order is one member and no second list (ADR 0041, ADR 0071).
/// <para>
/// The comparison ignores case, unlike the topic's ordinal one, and the asymmetry is each rule
/// following its source: a topic must arrive in the domain's canonical spelling because the index
/// stores that spelling, while a sort is translated to an enum member at the boundary and stored
/// nowhere — <c>?sort=newest</c> and <c>?sort=Newest</c> ask the same question.
/// </para>
/// <para>
/// A null passes, since absence is <see cref="RequiredAttribute"/>'s question and the default
/// order is an ordinary answer. So does a blank string, which is what an empty query parameter
/// binds to — <c>?sort=</c> asks for the default rather than for an order called nothing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class KnownSortAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> NamesByType = new();

    private readonly Type _orderType;

    /// <summary>
    /// Initializes the attribute against the enum that owns the order names.
    /// </summary>
    /// <param name="orderType">The order enum — the closed set of names the catalog publishes.</param>
    public KnownSortAttribute(Type orderType)
        : base("The {0} field must name an order this catalog publishes.")
    {
        _orderType = orderType;
    }

    /// <summary>
    /// Answers whether the value names an order the catalog publishes.
    /// </summary>
    /// <param name="value">The bound value, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="false"/> only for a non-blank string that no member answers to. Null and
    /// blank pass, and so does anything that is not a string — the framework convention for an
    /// attribute asked about a type it does not judge.
    /// </returns>
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string name when string.IsNullOrWhiteSpace(name) => true,
        string name => NamesOf(_orderType).Contains(name),
        _ => true
    };

    /// <summary>
    /// Every name the given enum declares, compared without case.
    /// </summary>
    private static IReadOnlySet<string> NamesOf(Type orderType) =>
        NamesByType.GetOrAdd(orderType, static type =>
        {
            if (!type.IsEnum)
            {
                throw new InvalidOperationException(
                    $"{type.Name} is not an enum, so [KnownSort] cannot read the names it is " +
                    "meant to refuse anything outside of.");
            }

            return Enum.GetNames(type).ToHashSet(StringComparer.OrdinalIgnoreCase);
        });
}
