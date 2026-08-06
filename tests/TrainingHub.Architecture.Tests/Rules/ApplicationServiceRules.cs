using System.Reflection;
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

    /// <summary>
    /// Every layered service signature, says which boundary it is on.
    /// </summary>
    /// <remarks>
    /// Two vocabularies meet at the layered stack's application services, and they look alike enough
    /// to be swapped by anybody reading quickly: `EditTrainerRequestHttp` is what a client sends,
    /// `TrainerEditionRequest` is what the application layer accepts after the mapping. The suffix
    /// is what tells them apart at the name alone — a `*Request` in, a `*Dto` out, against
    /// `*RequestHttp` and `*ResponseHttp` on the boundary above.
    /// <para>
    /// The population is the signatures rather than a folder, because the risk is not a badly named
    /// file: it is an HTTP contract or a domain type appearing where an application model belongs.
    /// A folder scan would miss both. Types the solution does not declare are skipped —
    /// <c>Guid</c>, <c>byte[]</c> and <c>CancellationToken</c> carry no layer to state — and the
    /// wrappers are unwrapped, so <c>Task&lt;Result&lt;TrainerDto&gt;&gt;</c> and
    /// <c>PagedResult&lt;TrainingDto&gt;</c> are read as the payload they carry.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "the layered application takes a *Request and answers a *Dto, so a name says which boundary " +
        "it belongs to — never confusable with the API's *RequestHttp and *ResponseHttp")]
    public void EveryLayeredServiceSignature_SaysWhichBoundaryItIsOn() =>
        Solution.LayeredApplication.DeclaredTypes()
            .Where(type => type.Name.EndsWith("ApplicationService", StringComparison.Ordinal))
            .SelectMany(service => service
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => (Service: service, Method: method)))
            .Selected("layered application service method")
            .SelectMany(entry => Ours(entry.Method.GetParameters().Select(parameter => parameter.ParameterType))
                .Where(type => !type.Name.EndsWith("Request", StringComparison.Ordinal))
                .Select(type => (entry, type, Half: "takes", Suffix: "Request"))
                .Concat(Ours(Unwrap(entry.Method.ReturnType))
                    .Where(type => !type.Name.EndsWith("Dto", StringComparison.Ordinal))
                    .Select(type => (entry, type, Half: "answers", Suffix: "Dto"))))
            .Select(found =>
                $"{found.entry.Service.Name}.{found.entry.Method.Name} {found.Half} " +
                $"{found.type.Name}, which does not end in {found.Suffix}. The layered application " +
                "takes a *Request and answers a *Dto; a name that says neither has to be read to be " +
                "placed, and the boundary above it is one suffix away (*RequestHttp, *ResponseHttp)")
            .ShouldHold();

    /// <summary>The types in a sequence that this solution declares, and therefore names.</summary>
    private static IEnumerable<Type> Ours(IEnumerable<Type> types) =>
        types.Where(type => Solution.Backend.Contains(type.Assembly));

    /// <summary>
    /// What a return type carries: <c>Task</c>, <c>Result</c> and <c>PagedResult</c> are envelopes,
    /// and a bare one carries nothing to name.
    /// </summary>
    private static IEnumerable<Type> Unwrap(Type answered) =>
        answered.IsGenericType
            ? answered.GetGenericArguments().SelectMany(Unwrap)
            : IsAnEnvelope(answered) ? [] : [answered];

    private static bool IsAnEnvelope(Type type) =>
        type.Name is "Task" or "ValueTask" or "Result" or "Void";
}
