using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TrainingHub.Shared.Api.Contracts;

/// <summary>
/// Refuses a status name the domain does not spell, on a filter a caller supplies.
/// </summary>
/// <remarks>
/// The allowed values are read off the domain type rather than restated here, and that is the whole
/// point of the attribute existing at all. A hand-written list at the boundary is the shape
/// <c>EveryNamedList_AgreesWithTheCode</c> exists to catch in prose (ADR 0041); writing one in an
/// annotation would be the same defect with a compiler in front of it. Adding a fourth status to
/// <c>TrainingStatus</c> makes it filterable here the same day, with nothing to remember.
/// <para>
/// Reflection is what that costs. The type is asked for a public static <c>GetStatuses()</c> and
/// each answer for its <c>Name</c> — the shape both status types have — resolved once per type and
/// cached for the process. A type that does not have it fails loudly the first time an action bound
/// to it runs, which both hosts' integration suites do on every build.
/// </para>
/// <para>
/// Applied to a property of a bound filter, <c>[ApiController]</c> answers a failure at model
/// binding with a <c>ValidationProblemDetails</c> naming the parameter, before the action runs and
/// before anything asks the domain to resolve the name. That is what lets <c>FromName</c> keep
/// throwing rather than answering a result: by the time it is called, the name is known good.
/// </para>
/// <para>
/// A null passes, since absence is <see cref="RequiredAttribute"/>'s question and a filter nobody
/// set is not a filter that failed. So does a blank string, which is what an empty query parameter
/// binds to — <c>?status=</c> asks for no filter rather than for a status called nothing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class KnownStatusAttribute : ValidationAttribute
{
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> NamesByType = new();

    private readonly Type _statusType;

    /// <summary>
    /// Initializes the attribute against the domain type that owns the names.
    /// </summary>
    /// <param name="statusType">The status value object — the one declaring <c>GetStatuses()</c>.</param>
    public KnownStatusAttribute(Type statusType)
        : base("The {0} field must name a status this API knows.")
    {
        _statusType = statusType;
    }

    /// <summary>
    /// Answers whether the value names a status the domain declares.
    /// </summary>
    /// <param name="value">The bound value, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="false"/> only for a non-blank string that no status answers to. Null and blank
    /// pass, and so does anything that is not a string — the framework convention for an attribute
    /// asked about a type it does not judge.
    /// </returns>
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string name when string.IsNullOrWhiteSpace(name) => true,
        string name => NamesOf(_statusType).Contains(name),
        _ => true
    };

    /// <summary>
    /// Every name the given status type declares, compared as the domain compares them.
    /// </summary>
    /// <remarks>
    /// Ordinal, because <c>FromName</c> is ordinal: an attribute that accepted <c>suspended</c>
    /// would hand the domain a name it refuses, and turn a <c>400</c> a caller can act on into a
    /// <c>500</c> two layers away.
    /// </remarks>
    private static IReadOnlySet<string> NamesOf(Type statusType) =>
        NamesByType.GetOrAdd(statusType, static type =>
        {
            var statuses = type.GetMethod("GetStatuses", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"{type.Name} declares no public static GetStatuses(), so [KnownStatus] cannot " +
                    "read the names it is meant to refuse anything outside of.");

            var name = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"{type.Name} declares no public Name, so [KnownStatus] cannot read what its " +
                    "statuses are called.");

            return ((IEnumerable)statuses.Invoke(null, null)!)
                .Cast<object>()
                .Select(status => (string)name.GetValue(status)!)
                .ToHashSet(StringComparer.Ordinal);
        });
}
