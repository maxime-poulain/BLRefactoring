using Microsoft.Extensions.Options;

namespace TrainingHub.Blazor.Bff;

/// <summary>
/// Asks Cloudflare's <c>siteverify</c> endpoint whether a token is genuine (ADR 0083).
/// </summary>
/// <remarks>
/// Fails closed, on purpose: a verification that cannot be completed — the service unreachable, an
/// answer that does not parse — refuses the message rather than waving it through, because the
/// window in which a guard is down is exactly the window a flood looks for. The visitor sees the
/// same refusal as a bot and simply tries again.
/// <para>
/// The secret travels only here, form-encoded the way the API documents, beside the token and the
/// visitor's address — the address so Cloudflare can refuse a token minted on one connection and
/// spent from another. Nothing about the visitor is kept: the answer is a boolean and this
/// adapter's memory ends with the request.
/// </para>
/// </remarks>
public sealed class TurnstileVerifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TurnstileOptions> options) : ITurnstileVerifier
{
    /// <summary>Named client this adapter speaks through, so tests can swap its far end.</summary>
    public const string ClientName = "Turnstile";

    /// <summary>The one address Cloudflare answers this question at.</summary>
    public const string SiteverifyPath = "/turnstile/v0/siteverify";

    /// <inheritdoc />
    public async Task<bool> IsGenuineAsync(string token, string? remoteIp, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var secret = options.Value.SecretKey;

        // The endpoint only asks when a pair is configured (ADR 0083); if this is ever reached
        // without one, the same closed-fail as an unreachable issuer applies.
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var challenge = new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token
        };

        if (!string.IsNullOrEmpty(remoteIp))
        {
            challenge["remoteip"] = remoteIp;
        }

        using var content = new FormUrlEncodedContent(challenge);

        try
        {
            var response = await httpClientFactory
                .CreateClient(ClientName)
                .PostAsync(SiteverifyPath, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var verdict = await response.Content
                .ReadFromJsonAsync<SiteverifyAnswer>(cancellationToken);

            return verdict?.Success ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>The one field of Cloudflare's answer this host acts on.</summary>
    private sealed record SiteverifyAnswer(bool Success);
}
