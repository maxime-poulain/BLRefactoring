namespace TrainingHub.Blazor.Bff;

/// <summary>
/// The pair of keys Cloudflare Turnstile issues for one site (ADR 0083).
/// </summary>
/// <remarks>
/// The site key is public by design — it is rendered into the widget every visitor loads — and the
/// secret key never leaves this host: it rides only on the server-side call that asks Cloudflare
/// whether a token is genuine.
/// <para>
/// The pair is optional, and its absence is a decision this host honors: with neither key
/// configured the challenge is off — the contact endpoint forwards without judging a token, the
/// browser is told there is no widget to render, and the honeypot and both rate windows keep
/// standing (ADR 0082). What is refused at startup is half a pair: one key without the other is
/// not a smaller configuration, it is a broken one, so the two travel together or not at all
/// (ADR 0033). A deployment that wants the challenge supplies both, through configuration that
/// never enters the repository — <c>appsettings.Local.json</c> for a developer's own keys
/// (ADR 0035), environment variables for a deployment's.
/// </para>
/// </remarks>
public sealed class TurnstileOptions
{
    /// <summary>Configuration section the options bind from.</summary>
    public const string SectionName = "Turnstile";

    /// <summary>The public key the widget renders with, handed to the browser on request.</summary>
    public string? SiteKey { get; set; }

    /// <summary>The private key this host proves its identity with when verifying a token.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Whether the pair is present, and the challenge therefore on.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
