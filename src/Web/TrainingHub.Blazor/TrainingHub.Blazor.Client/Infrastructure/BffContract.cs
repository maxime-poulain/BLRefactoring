namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// What the front end and its BFF agree on: one header, and the shape of an identity.
/// </summary>
/// <remarks>
/// Declared here, in the WebAssembly project, and used by the host — which references it — rather
/// than declared twice and kept in step by hand. The header in particular is a security control:
/// two copies drifting apart would not fail a build, it would silently stop refusing forged
/// requests. See ADR 0009.
/// </remarks>
public static class BffContract
{
    /// <summary>
    /// The header the front end sends on every call it makes, and the only thing that admits a
    /// write to the BFF.
    /// </summary>
    /// <remarks>
    /// Cookies travel automatically, which is what reintroduces cross-site request forgery — the
    /// one attack bearer tokens were immune to. A cross-site form, image or navigation cannot set a
    /// custom header, and a cross-origin script attempting one faces a preflight this host never
    /// approves. Together with <c>SameSite=Strict</c>, that is the mitigation.
    /// <para>
    /// The sentence above has a second half, and missing it cost this application every portrait it
    /// ever tried to display: <em>our</em> images cannot set a custom header either. An
    /// <c>&lt;img&gt;</c> is issued by the browser, not by the code that wrote the tag, so a
    /// subresource of this very page arrived without this header and was refused. The guard
    /// therefore admits a safe read the browser itself attests came from this origin, and this
    /// header remains what admits everything else (ADR 0063).
    /// </para>
    /// </remarks>
    public const string RequestedWithHeader = "X-Requested-With";

    /// <summary>Value carried by <see cref="RequestedWithHeader"/>.</summary>
    public const string RequestedWithValue = "TrainingHub.Blazor";
}

/// <summary>The signed-in caller, as the BFF reports them.</summary>
public sealed record BffUser(IReadOnlyList<BffClaim> Claims);

/// <summary>One claim, flattened for transport.</summary>
public sealed record BffClaim(string Type, string Value);
