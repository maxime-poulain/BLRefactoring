using System.Net;
using System.Net.Http.Json;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Signing in and out, through the BFF.
/// </summary>
/// <remarks>
/// The generated client is not used for this, and cannot be: the API answers <c>/Auth/login</c>
/// with the token, and a generated method returning it would put the credential straight back in
/// the browser — the thing ADR 0009 exists to prevent. The BFF's own endpoint answers with a cookie
/// and no body.
/// </remarks>
public sealed class BffSessionClient(IHttpClientFactory httpClientFactory) : IBffSessionClient
{
    /// <summary>
    /// Signs in through the BFF, which keeps the token server-side and answers with a cookie.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/login",
            new LoginHttpRequest { Username = username, Password = password },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    /// <summary>
    /// Creates an account through the BFF, whose endpoint is the one a visitor can reach.
    /// </summary>
    /// <remarks>
    /// The generated client points at <c>/api/Auth/register</c>, which the proxy's catch-all route
    /// answers with a 401 for anyone without a session — and a visitor creating an account never
    /// has one. The BFF forwards the call itself and passes the API's problem document through,
    /// because the per-field messages inside it are what the form renders.
    /// </remarks>
    public async Task<ProblemDetails?> RegisterAsync(
        RegisterHttpRequest registration,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync("bff/register", registration, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.Ordinal) == true)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (problem is not null)
            {
                return problem;
            }
        }

        // A refusal with no readable document — a gateway's HTML page, an empty body — is an
        // outage to report, not a validation verdict to render.
        throw new HttpRequestException(
            $"Registration answered {(int)response.StatusCode} without a problem document.");
    }

    /// <summary>
    /// Asks for a reset link. The answer never says whether the address has an account.
    /// </summary>
    public async Task<bool> ForgotPasswordAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/forgot-password",
            new ForgotPasswordHttpRequest { Email = emailAddress },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    /// <summary>
    /// Redeems a reset link against a new password, through the BFF.
    /// </summary>
    /// <remarks>
    /// Registration's shape, refusal and all, with one addition: the recovery window's 429
    /// carries no document, so it is translated into one here rather than thrown — being told to
    /// wait is a verdict the form should render, not an outage to report.
    /// </remarks>
    public async Task<ProblemDetails?> ResetPasswordAsync(
        ResetPasswordHttpRequest reset,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync("bff/reset-password", reset, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            return new ProblemDetails
            {
                Detail = "Too many attempts. Wait a few minutes and try again."
            };
        }

        if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.Ordinal) == true)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (problem is not null)
            {
                return problem;
            }
        }

        throw new HttpRequestException(
            $"The password reset answered {(int)response.StatusCode} without a problem document.");
    }

    /// <summary>
    /// Redeems a verification link, through the BFF.
    /// </summary>
    /// <remarks>
    /// Sign-in's shape rather than the reset redemption's, because the BFF passes only the
    /// status through: the API answers every dead link with one fixed sentence, and the page
    /// renders its own localized version of it, so there is no document to read (ADR 0090).
    /// </remarks>
    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/verify-email",
            new VerifyEmailHttpRequest { Token = token },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    /// <summary>
    /// Erases the account, and with it the session that asked.
    /// </summary>
    /// <remarks>
    /// Registration's shape on the way back: a refusal passes through as the problem document
    /// whose per-field messages the dialog renders — the wrong password keyed to its field first
    /// of all. On success the BFF has already signed the session out, so there is nothing to end
    /// here; the caller refreshes the authentication state and leaves.
    /// </remarks>
    public async Task<ProblemDetails?> EraseAccountAsync(string password, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/erase-account",
            new EraseAccountHttpRequest { Password = password },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.Ordinal) == true)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (problem is not null)
            {
                return problem;
            }
        }

        throw new HttpRequestException(
            $"The account erasure answered {(int)response.StatusCode} without a problem document.");
    }

    /// <summary>
    /// Ends the session.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsync("bff/logout", content: null, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            // The session had already expired. Signing out of nothing succeeded: the caller wants
            // to end up signed out, and they are.
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Signing in and out, as the pages see it.</summary>
public interface IBffSessionClient
{
    /// <summary>
    /// Signs in. <see langword="false"/> means the credentials were refused — an ordinary outcome,
    /// not an exception.
    /// </summary>
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account. <see langword="null"/> means it was created; a problem document is the
    /// API's refusal, carrying the per-field messages the form renders.
    /// </summary>
    /// <param name="registration">The account to create.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<ProblemDetails?> RegisterAsync(
        RegisterHttpRequest registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks for a reset link. <see langword="true"/> means the request was taken — which is all
    /// anyone is told; <see langword="false"/> means the recovery window refused it for now.
    /// </summary>
    /// <param name="emailAddress">The address whose account wants its password reset.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> ForgotPasswordAsync(string emailAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a reset link. <see langword="null"/> means the password was changed; a problem
    /// document is the refusal, carrying the messages the form renders.
    /// </summary>
    /// <param name="reset">The address, the emailed token, and the new password twice.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<ProblemDetails?> ResetPasswordAsync(
        ResetPasswordHttpRequest reset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a verification link. <see langword="true"/> means the address is proven;
    /// <see langword="false"/> means the link is dead — one answer for every way it can be,
    /// which is all the API tells anyone (ADR 0090).
    /// </summary>
    /// <param name="token">The verification token, exactly as the emailed link carried it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases the account. <see langword="null"/> means it is gone and the session with it; a
    /// problem document is the refusal, the wrong password keyed to its field.
    /// </summary>
    /// <param name="password">The account's current password — the proof of intent.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<ProblemDetails?> EraseAccountAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
