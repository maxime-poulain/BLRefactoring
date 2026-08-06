using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the domain's own types are made of, and what a read model may become after it is read.
/// </summary>
/// <remarks>
/// ADR 0009 made typed identifiers a decision and ADR 0032 decided how a value object reaches its
/// table. Neither was ever asked whether the values a value object <em>holds</em> are themselves
/// domain types, or whether the text it stores is the text it was given. Both questions had one
/// answer in the code written after the records and another in the code written before, which is
/// the drift ADR 0044 converts into rules.
/// </remarks>
public sealed class DomainVocabularyRules
{
    /// <summary>
    /// The value objects the domain declares, identifiers excluded.
    /// </summary>
    /// <remarks>
    /// An <c>EntityId</c> is a value object whose whole purpose is to wrap a <c>Guid</c>, so it is
    /// the one type that must expose one. Sweeping it up would make the first rule below demand
    /// that a typed identifier be built out of a typed identifier.
    /// </remarks>
    private static IEnumerable<Type> ValueObjects =>
        Solution.Domain
            .DeclaredTypes()
            .Where(type => typeof(ValueObject).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => !IsAnIdentifier(type));

    /// <summary>
    /// No value object, exposes a bare guid.
    /// </summary>
    /// <remarks>
    /// <c>TrainerPhoto.PhotoId</c> was the one, in the aggregate of a repository whose README opens
    /// its identifier section with the argument that a <c>TrainerId</c> must not be passable where a
    /// <c>TrainingId</c> is expected. It is also the identifier the object key is built from —
    /// <c>trainers/{trainerId}/{photoId}</c> — so the one place two identifiers sit side by side was
    /// the one place only one of them was typed. See ADR 0044.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0044",
        "a value a domain type stores is a domain type: an identifier is typed, or it is confusable with every other")]
    public void NoValueObject_ExposesABareGuid() =>
        ValueObjects
            .Selected("value object")
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(Guid)
                                   || property.PropertyType == typeof(Guid?))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Select(member =>
                $"{member} is a bare Guid on a value object. A typed identifier cannot be passed " +
                "where another is expected, which is the whole of ADR 0009 — give it its own type")
            .ShouldHold();

    /// <summary>
    /// Every text value object, trims what it stores.
    /// </summary>
    /// <remarks>
    /// Leading and trailing whitespace is presentation, not content: a value object that keeps it
    /// makes <c>"Bob "</c> and <c>"Bob"</c> two different trainers, and makes a uniqueness rule
    /// answer differently depending on how carefully somebody typed. Five value objects trimmed and
    /// two did not, with no record and no comment saying which was the rule — the difference was
    /// when each was written.
    /// <para>
    /// <c>Email</c> is the exception and carries its own reason: an address is refused rather than
    /// repaired, because the domain cannot know whether the space was a typo or part of a quoted
    /// local part it is not entitled to rewrite. The exemption is spelled here rather than inferred,
    /// so that a second one has to be argued for.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0044",
        "a text value object trims what it stores, so that two spellings of one value are one value")]
    public void EveryTextValueObject_TrimsWhatItStores() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/TrainingHub.Shared.Domain/", StringComparison.Ordinal))
            .Where(file => file.Contains("/ValueObjects/", StringComparison.Ordinal))
            .Selected("value object source file")
            .Select(file => (
                File: file,
                Text: SourceTree.ReadText(Path.Combine(
                    SourceTree.RepositoryRoot,
                    file.Replace('/', Path.DirectorySeparatorChar)))))
            .Where(entry => !entry.File.EndsWith("/Email.cs", StringComparison.Ordinal))
            .Where(entry => entry.Text.Contains("Create(string", StringComparison.Ordinal))
            .Where(entry => !entry.Text.Contains("Trim()", StringComparison.Ordinal))
            .Select(entry =>
                $"'{entry.File}' builds itself from a string and never trims it. Two spellings of " +
                "one value are two values, in equality and in every uniqueness rule that reads them")
            .ShouldHold();

    /// <summary>
    /// Every application read model, is built once.
    /// </summary>
    /// <remarks>
    /// A <c>*Dto</c> is what a query answered — the state of the database at the moment it was
    /// asked. A settable property lets a caller change that answer and hand it on, so the next
    /// reader is holding something no query ever returned. <c>TrainerDto</c> was written with
    /// <c>init</c> and <c>TrainingDto</c> with <c>set</c>; nothing said which was intended.
    /// <para>
    /// <c>init</c> is detected the way the compiler emits it — a modreq of <c>IsExternalInit</c> on
    /// the setter's return parameter — rather than by reading the source, so a property that only
    /// looks immutable does not pass.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0044",
        "an application read model is built once: what a query answered is what the database said")]
    public void EveryApplicationReadModel_IsBuiltOnce() =>
        Solution.Application
            .DeclaredTypes()
            .Where(type => type.Name.EndsWith("Dto", StringComparison.Ordinal) && !type.IsAbstract)
            .Selected("application read model")
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
                .Select(property =>
                    $"{type.Name}.{property.Name} can be set after the query answered. A read model " +
                    "a caller can edit is a read model that can report something no query returned"))
            .ShouldHold();

    /// <summary>Whether a setter is <c>init</c> rather than <c>set</c>.</summary>
    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.Name == "IsExternalInit");

    /// <summary>Whether a type is one of the typed identifiers, whatever it is called.</summary>
    private static bool IsAnIdentifier(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityId<>))
            {
                return true;
            }
        }

        return false;
    }
}
