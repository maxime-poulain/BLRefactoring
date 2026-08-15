namespace TrainingHub.Blazor.Bff;

/// <summary>
/// Judges whether a Turnstile token was earned by a person in a browser (ADR 0083).
/// </summary>
/// <remarks>
/// A port for the one outbound call this host makes to somebody else's service, so the contact
/// endpoint can be tested against a seam rather than against Cloudflare. Its single consumer is
/// the contact endpoint; nothing else on this host asks the question.
/// </remarks>
public interface ITurnstileVerifier
{
    /// <summary>
    /// Whether <paramref name="token"/> is a genuine, unspent answer to this site's challenge.
    /// </summary>
    /// <param name="token">The token the widget minted in the visitor's browser.</param>
    /// <param name="remoteIp">The visitor's address, forwarded so Cloudflare can bind the token
    /// to the connection it was minted on; <see langword="null"/> when the host cannot see one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> IsGenuineAsync(string token, string? remoteIp, CancellationToken cancellationToken);
}
