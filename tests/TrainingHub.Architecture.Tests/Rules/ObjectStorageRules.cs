using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Storage;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What ADR 0021 claims about where photos live, held to.
/// </summary>
/// <remarks>
/// Two claims, and they are the two the record actually rests on: that changing storage provider
/// is configuration rather than code, and that a committed row can never name bytes that are gone.
/// Both are the kind of thing that stays true for exactly as long as nobody adds a convenient
/// shortcut.
/// </remarks>
public sealed class ObjectStorageRules
{
    // The namespace, not the package name. NetArchTest matches what types are declared in, and
    // AWSSDK.S3 declares Amazon.S3 and Amazon.Runtime — so a rule written against "AWSSDK" matches
    // nothing at all and passes everywhere, including where it should not.
    private const string StorageProtocolNamespace = "Amazon";

    /// <summary>
    /// Only the infrastructure, knows the object store.
    /// </summary>
    [Fact]
    [ArchitectureRule("0021",
        "one project speaks the storage protocol, so changing provider is a configuration value and not a rewrite")]
    public void OnlyTheInfrastructure_KnowsTheObjectStore()
    {
        // Every backend assembly but the one adapter's. The claim is not that some layer is
        // insulated — it is that exactly one place in the product has ever heard of S3, which is
        // what makes the provider swappable at all.
        //
        // Backend rather than everything, and the difference is not a loophole. This rule was
        // written against Solution.All and immediately failed on the unit tests for the adapter,
        // which stand in for IAmazonS3 — as a test of an adapter must, since the boundary is the
        // subject. A rule that forbids testing the one type it is about would be answered by
        // deleting the test, which is the opposite of what it is for.
        var everywhereElse = Solution.Backend
            .Where(assembly => assembly != Solution.Infrastructure)
            .ToArray();

        foreach (var assembly in everywhereElse)
        {
            Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(StorageProtocolNamespace)
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// The object store, offers no way to overwrite in place.
    /// </summary>
    /// <remarks>
    /// The interface is held to exactly three operations because the fourth is the dangerous one.
    /// Replacing a photo is write-new, commit, delete-old — three steps whose order is the reason
    /// a crash leaves collectable rubbish rather than a profile pointing at nothing. A
    /// <c>Replace</c> method would read as one atomic step, would be implemented as an overwrite in
    /// place, and would quietly make the ordering unavailable to anyone who called it.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0021",
        "replacing is writing a new object and deleting the old one afterwards, so nothing offers to overwrite in place")]
    public void TheObjectStore_OffersNoWayToOverwriteInPlace()
    {
        string[] expected = ["PutAsync", "GetAsync", "DeleteAsync"];

        var declared = typeof(IObjectStore)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        declared
            .Selected("operation on IObjectStore")
            .Where(operation => !expected.Contains(operation, StringComparer.Ordinal))
            .Select(operation =>
                $"IObjectStore declares '{operation}'. It holds bytes under a key and does three " +
                "things to them; a fourth is either a shortcut around the write ordering or belongs " +
                "to whatever sits on top of it")
            .Concat(expected
                .Where(operation => !declared.Contains(operation, StringComparer.Ordinal))
                .Select(operation =>
                    $"IObjectStore no longer declares '{operation}', so this rule is now guarding " +
                    "an interface that has moved out from under it"))
            .ShouldHold();
    }
}
