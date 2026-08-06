using System.Reflection;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common.Errors;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Who is allowed to name an error, and what the name has to say.
/// </summary>
/// <remarks>
/// The codes used to be a closed set: a smart enum whose fourteen members were the only codes that
/// could exist, and a typo could not compile. They are now strings on an open type, because each
/// aggregate declares its own and the kernel has no business knowing them. That trade buys the
/// dependency rule and costs the compiler's help — these rules are what buys it back.
/// <para>
/// Without them, <c>"Traning.DuplicateTitle"</c> compiles, ships, and is discovered by whoever was
/// branching on the correct spelling.
/// </para>
/// </remarks>
public sealed partial class ErrorVocabularyRules
{
    /// <summary>
    /// A reference to the kernel's own holder, and not to an aggregate's.
    /// </summary>
    /// <remarks>
    /// The lookbehind is the whole rule: <c>TrainerErrorCodes.InvalidEmail</c> ends in the same
    /// eleven characters as <c>ErrorCodes.Unspecified</c>, and a scan without it would forbid every
    /// aggregate from naming its own codes — the opposite of ADR 0015.
    /// </remarks>
    [GeneratedRegex(@"(?<![A-Za-z0-9_])ErrorCodes\.")]
    private static partial Regex KernelCodeReference { get; }

    // Spelled in two halves, like the assertion-library rule for the same reason: a rule that
    // forbids a token cannot write it, or it finds itself and stays red forever.
    private const string InlineConstruction = "new Error" + "Code(";

    private static IReadOnlyList<Type> Holders { get; } =
    [
        .. Solution.All
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
    ];

    // The lambda parameter is not called `field`, and in this member it could not be: C# 14 made
    // `field` a contextual keyword inside a property accessor, where it binds to the synthesized
    // backing field rather than to the parameter. The identical lambda in a method body below
    // compiles either way; both are spelled the same here so the difference does not read as an
    // oversight.
    private static IEnumerable<(Type Holder, FieldInfo Field, ErrorCode Code)> Declared =>
        Holders.SelectMany(holder => holder
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(declaration => declaration.FieldType == typeof(ErrorCode))
            .Select(declaration => (holder, declaration, (ErrorCode)declaration.GetValue(null)!)));

    /// <summary>
    /// No domain type, raises a kernel code.
    /// </summary>
    /// <remarks>
    /// The five rules around this one judge what a code's value looks like and where it is
    /// declared. None of them can see who <em>raises</em> one, which is how <c>Name.Create</c> came
    /// to answer a one-letter firstname with <c>ErrorCodes.Unspecified</c> — a code that means
    /// "something went wrong", from the one layer that always knows what went wrong and whose
    /// aggregate is right there to own it. It was the only such call under <c>src/</c>.
    /// <para>
    /// The kernel's codes are for callers with no aggregate to name: the validation pipeline
    /// (ADR 0016), and the boundary. Inside the domain there is always an owner. See ADR 0044.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0044",
        "no domain type raises a kernel code: inside the domain there is always an aggregate to own the refusal")]
    public void NoDomainType_RaisesAKernelCode() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/TrainingHub.Shared.Domain/", StringComparison.Ordinal))
            .Selected("domain source file")
            .SelectMany(file => SourceTree
                .ReadLines(Path.Combine(SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar)))
                .Select((line, number) => (line: line.TrimStart(), number))
                .Where(entry => !entry.line.StartsWith("//", StringComparison.Ordinal))
                .Where(entry => KernelCodeReference.IsMatch(entry.line))
                .Select(entry =>
                    $"'{file}' line {entry.number + 1} raises a kernel code. ADR 0015 gives each " +
                    "aggregate the codes it raises, and a domain refusal always has an owner — " +
                    "declare it on the aggregate's own holder"))
            .ShouldHold();

    /// <summary>
    /// Every code, is declared on a holder.
    /// </summary>
    [Fact]
    [ArchitectureRule("0015",
        "a code is declared by a holder, so that the set of codes is a set somebody can read")]
    public void EveryCode_IsDeclaredOnAHolder() =>
        Solution.All
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Selected("type")
            .Where(type => !type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(type => type
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(declaration => declaration.FieldType == typeof(ErrorCode))
                .Select(declaration =>
                    $"{type.FullName}.{declaration.Name} declares an error code outside a *ErrorCodes " +
                    "holder, where nobody looking for the vocabulary would find it"))
            .ShouldHold();

    /// <summary>
    /// No code, is built at a call site.
    /// </summary>
    [Fact]
    [ArchitectureRule("0015",
        "nothing builds a code inline: a code that no holder declares is a code nobody can grep for")]
    public void NoCode_IsBuiltAtACallSite() =>
        // A source scan, because this is about call sites and a call site is not visible through
        // reflection. It is the rule that actually replaces what the closed set used to give for
        // free — the other three check declarations, and a misspelling never gets declared.
        SourceTree.SourceFiles
            .Selected("source file")
            .Select(SourceTree.Relative)
            .Where(file => !file.EndsWith("ErrorCodes.cs", StringComparison.Ordinal))
            .Where(file => SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, file))
                .Contains(InlineConstruction, StringComparison.Ordinal))
            .Select(file =>
                $"'{file}' constructs an error code where it stands. Declare it on the holder that " +
                "owns it — inline, a misspelling is a new code and nothing says so")
            .ShouldHold();

    /// <summary>
    /// Every domain code, names its owner.
    /// </summary>
    [Fact]
    [ArchitectureRule("0015",
        "a code declared by an aggregate carries that aggregate's name, so no two owners collide")]
    public void EveryDomainCode_NamesItsOwner() =>
        Declared
            .Where(entry => entry.Holder.Assembly == Solution.Domain)
            .Selected("code declared by an aggregate")
            .Where(entry => !entry.Code.Value.StartsWith(
                $"{entry.Holder.Name[..^"ErrorCodes".Length]}.", StringComparison.Ordinal))
            .Select(entry =>
                $"{entry.Holder.Name}.{entry.Field.Name} publishes '{entry.Code.Value}', which does " +
                $"not begin with '{entry.Holder.Name[..^"ErrorCodes".Length]}.'")
            .ShouldHold();

    /// <summary>
    /// Every kernel code, belongs to nobody.
    /// </summary>
    [Fact]
    [ArchitectureRule("0015",
        "the kernel declares only what belongs to nobody, so its codes carry no owner at all")]
    public void EveryKernelCode_BelongsToNobody() =>
        Declared
            .Where(entry => entry.Holder.Assembly == Solution.Kernel)
            .Selected("code declared by the kernel")
            .Where(entry => entry.Code.Value.Contains('.', StringComparison.Ordinal))
            .Select(entry =>
                $"the kernel declares '{entry.Code.Value}', which names an owner. A code the kernel " +
                "holds is one that is true of any aggregate — if it has an owner, it belongs with them")
            .ShouldHold();

    /// <summary>
    /// No two codes, share a value.
    /// </summary>
    [Fact]
    [ArchitectureRule("0015",
        "two codes never share a value, or a client branching on one gets the other")]
    public void NoTwoCodes_ShareAValue() =>
        Declared
            .Selected("declared code")
            .GroupBy(entry => entry.Code.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"'{group.Key}' is declared {group.Count()} times: " +
                string.Join(", ", group.Select(entry => $"{entry.Holder.Name}.{entry.Field.Name}")))
            .ShouldHold();
}
