namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Carries one Turnstile token from the dialog that earned it to the request that spends it
/// (ADR 0083).
/// </summary>
/// <remarks>
/// The generated client's signatures have no room for a header, so the page deposits the token
/// here and <see cref="TurnstileTokenHandler"/> attaches it to the next outgoing request — the
/// same division of labor as <see cref="RequestedWithHandler"/>, with one difference: this header
/// is per-message rather than universal, which is why the token is taken rather than read. A
/// token is single-use on Cloudflare's side anyway; a second request wearing the same one would
/// be refused, so nothing is lost by forgetting it at the first spend.
/// <para>
/// A singleton with a mutable field is safe where this runs: the WebAssembly application is
/// single-threaded, and the deposit and the spend are two steps of one user action.
/// </para>
/// </remarks>
public sealed class TurnstileTokenAccessor
{
    private string? _token;

    /// <summary>Deposits the token the widget minted, for the next request to spend.</summary>
    /// <remarks>
    /// A <see langword="null"/> deposit is a no-op rather than an error: a draft made while the
    /// challenge is off carries no token, and the request that follows honestly carries no header
    /// (ADR 0083).
    /// </remarks>
    /// <param name="token">The token, exactly as the widget answered it, or nothing.</param>
    public void Deposit(string? token)
    {
        if (token is not null)
        {
            _token = token;
        }
    }

    /// <summary>Answers the deposited token and forgets it, or nothing when none waits.</summary>
    public string? Take()
    {
        var token = _token;
        _token = null;

        return token;
    }
}
