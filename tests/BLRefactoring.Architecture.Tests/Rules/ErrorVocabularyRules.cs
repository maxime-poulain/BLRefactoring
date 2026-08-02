using System.Reflection;
using BLRefactoring.Architecture.Tests.Framework;
using BLRefactoring.Shared.Common.Errors;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

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
public sealed class ErrorVocabularyRules
{
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
