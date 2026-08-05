using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the layered stack's services owe their readers: a name that says the layer.
/// </summary>
/// <remarks>
/// The naming vocabulary spells the layer into every service's name: <c>*DomainService</c> in the
/// domain (ADR 0036, held by <c>EveryDomainService_ExistsByRecord</c>), <c>*CommandHandler</c> and
/// <c>*QueryHandler</c> in the CQRS stack, adapters named after their technology in the
/// infrastructure. This rule holds the layered stack's corner of that vocabulary — until it, the
/// <c>ApplicationService</c> suffix was a convention of fact that a <c>TrainerService</c> could
/// quietly dilute.
/// </remarks>
public sealed class ApplicationServiceRules
{
    /// <summary>
    /// The layered application, names its services in full.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a layered service carries the ApplicationService suffix in full, so the name says the layer " +
        "— the mirror of the domain's *DomainService (ADR 0036)")]
    public void TheLayeredApplication_NamesItsServicesInFull() =>
        Solution.LayeredApplication.DeclaredTypes()
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Selected("layered application service")
            .Where(service => !service.Name.EndsWith("ApplicationService", StringComparison.Ordinal))
            .Select(service =>
                $"{service.Name} does not end in ApplicationService. The suffix is the convention " +
                "that tells a reader, at the name alone, which layer they are in — the same bargain " +
                "*DomainService strikes for the domain (ADR 0036)")
            .ShouldHold();
}
