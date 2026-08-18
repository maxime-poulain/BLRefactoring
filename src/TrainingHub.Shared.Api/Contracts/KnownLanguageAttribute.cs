using System.ComponentModel.DataAnnotations;
using TrainingHub.Translations;

namespace TrainingHub.Shared.Api.Contracts;

/// <summary>
/// Refuses a language this application does not answer in.
/// </summary>
/// <remarks>
/// <see cref="KnownTopicAttribute"/>'s sibling, without its reflection: the allowed values are
/// <c>SupportedLanguages.All</c>, and this layer may name that type — the resource assembly is
/// reachable from every surface and forbidden only to the inner circles (ADR 0088). One list, so
/// a language the selector offers and the boundary refuses cannot happen.
/// <para>
/// The comparison is case-insensitive, unlike the topic attribute's: a culture code is not a
/// spelling somebody chose, and <c>FR</c> names the same language as <c>fr</c> to every framework
/// that will read it. A null or blank passes — absence is <see cref="RequiredAttribute"/>'s
/// question, and this attribute answers only "is that a language".
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class KnownLanguageAttribute : ValidationAttribute
{
    /// <summary>Initializes the attribute with the message a refusal carries.</summary>
    public KnownLanguageAttribute()
        : base("The {0} field must name a language this application answers in.")
    {
    }

    /// <summary>Answers whether the value names a supported language.</summary>
    /// <param name="value">The bound value, possibly <see langword="null"/>.</param>
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string language when string.IsNullOrWhiteSpace(language) => true,
        string language => SupportedLanguages.All.Contains(language, StringComparer.OrdinalIgnoreCase),
        _ => true
    };
}
