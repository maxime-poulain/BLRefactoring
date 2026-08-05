using TrainingHub.Architecture.Tests.Framework;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What ADR 0031 claims about how email leaves the system, held to.
/// </summary>
/// <remarks>
/// One claim, because it is the one the record actually rests on: that changing mail provider is
/// configuration rather than code. The behavioural half — that a committed fact really leaves the
/// host as an SMTP message — is proven where behaviour is proven, by <c>EmailTest</c> in the
/// shared kit, against a real server.
/// </remarks>
public sealed class EmailDeliveryRules
{
    // The namespaces, not the package name. NetArchTest matches what types are declared in, and
    // MailKit declares MailKit.* and MimeKit.* — so a rule written against the package id alone
    // would miss the MIME half, and every message is composed in that half before it is sent.
    private static readonly string[] MailProtocolNamespaces = ["MailKit", "MimeKit"];

    /// <summary>
    /// Only the infrastructure, speaks SMTP.
    /// </summary>
    [Fact]
    [ArchitectureRule("0031",
        "one project speaks the mail protocol, so changing provider is a configuration value and not a rewrite")]
    public void OnlyTheInfrastructure_SpeaksSmtp()
    {
        // Every backend assembly but the one adapter's, the same line the object store holds for
        // "Amazon": the claim is not that some layer is insulated — it is that exactly one place
        // in the product has ever heard of the mail client, which is what makes the provider
        // swappable at all. The application layer owns the port; the moment it also named the
        // client, the port would be decoration around a dependency it exists to prevent.
        var everywhereElse = Solution.Backend
            .Where(assembly => assembly != Solution.Infrastructure)
            .ToArray();

        foreach (var assembly in everywhereElse)
        {
            Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(MailProtocolNamespaces)
                .GetResult()
                .ShouldHold();
        }
    }
}
