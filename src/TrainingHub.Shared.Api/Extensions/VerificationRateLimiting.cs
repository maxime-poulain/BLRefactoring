using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// The window bounding how often one account can ask for its verification email (ADR 0090).
/// </summary>
/// <remarks>
/// <see cref="ContactRateLimiting"/>'s mold with a partition the contact form could not have:
/// the resend route is authenticated, so the caller's account identity is real here — signed
/// into the bearer token — where a caller address never is behind the BFF. One permit is one
/// email <em>and one revocation of the earlier link</em> (latest-only), so the window bounds two
/// things at once: the mail an account's inbox can be made to receive, and how fast a hostile
/// session could keep invalidating the link the account's owner is trying to click.
/// <para>
/// The verify endpoint deliberately has no window of its own: a redemption costs one indexed
/// SELECT, and guessing 256 bits is not a plan a rate limit needs to have an answer for.
/// </para>
/// </remarks>
public static class VerificationRateLimiting
{
    /// <summary>
    /// The name the resend action names in its <c>[EnableRateLimiting]</c>.
    /// </summary>
    public const string PolicyName = "resend-verification-per-account";

    /// <summary>
    /// How many verification emails one account may ask for within a window.
    /// </summary>
    /// <remarks>
    /// Generous for a person and useless for abuse: five covers a full inbox-hunting session —
    /// spam folder, typo doubts, a slow relay — and caps the self-addressed mail at four hundred
    /// eighty messages a day, all of them to the account's own address.
    /// </remarks>
    public const int PermitsPerWindow = 5;

    /// <summary>The length of the window, in minutes.</summary>
    public const int WindowMinutes = 15;

    /// <summary>
    /// Declares the resend window, for each host to hand to <c>AddRateLimiter</c>.
    /// </summary>
    /// <remarks>
    /// A refusal answers 429 and nothing else, for the contact window's reason: what a caller may
    /// know is that they were refused, not the shape of the window.
    /// </remarks>
    /// <param name="options">The limiter options the host is building.</param>
    public static void Configure(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
            Account(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = TimeSpan.FromMinutes(WindowMinutes),
                // No queue, for the contact window's reason: a caller waiting on a window they
                // cannot see is worse served than one told plainly to check the emails they have.
                QueueLimit = 0
            }));
    }

    /// <summary>
    /// The account this request is from, as the partition key.
    /// </summary>
    /// <remarks>
    /// The user-id claim the authorization pipeline has already verified. A request without one
    /// falls into one shared partition rather than into none — an unpartitioned fallback would be
    /// a way past the window — and can only be a misconfiguration anyway, since the route sits
    /// behind the trainer policy.
    /// </remarks>
    private static string Account(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
