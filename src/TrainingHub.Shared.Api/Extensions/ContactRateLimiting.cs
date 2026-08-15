using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// The window bounding how much mail one trainer can be made to receive (ADR 0082).
/// </summary>
/// <remarks>
/// The first rate limiting in this application, and it is deliberately partitioned by the
/// <em>recipient</em> rather than by the caller. Both API hosts sit behind the BFF, nothing here
/// installs <c>UseForwardedHeaders</c> and nothing reads <c>X-Forwarded-For</c>, so every browser's
/// request arrives wearing the proxy's address: a per-caller window at this layer would be one
/// global window that the first busy visitor closes for everybody.
/// <para>
/// Partitioning by the route's trainer is not spoofable and bounds the thing worth bounding — a
/// person being buried in messages. The other half, bounding how much one visitor can send, lives
/// where the visitor's address is real: the BFF.
/// </para>
/// <para>
/// The window is declared here rather than in either host, for the reason every shared piece of
/// this boundary is: both hosts must publish the same operation with the same behavior, and a
/// window one of them wrote differently would be a difference no contract test could see. Only the
/// registration call itself stays in each <c>Program.cs</c>, where the rest of the wiring is.
/// </para>
/// </remarks>
public static class ContactRateLimiting
{
    /// <summary>
    /// The name the contact action names in its <c>[EnableRateLimiting]</c>.
    /// </summary>
    public const string PolicyName = "contact-trainer";

    /// <summary>
    /// How many messages one trainer may be sent within a window.
    /// </summary>
    /// <remarks>
    /// Generous for a person and useless for a flood: nobody legitimately receives twenty messages
    /// a minute through a public form, and nobody legitimately sends the twenty-first either.
    /// </remarks>
    public const int PermitsPerWindow = 20;

    /// <summary>The length of the window, in seconds.</summary>
    public const int WindowSeconds = 60;

    /// <summary>The route value the partition is taken from.</summary>
    private const string TrainerRouteValue = "trainerId";

    /// <summary>
    /// Declares the contact window, for each host to hand to <c>AddRateLimiter</c>.
    /// </summary>
    /// <remarks>
    /// A refusal answers 429 and nothing else. It carries no <c>Retry-After</c> and no problem
    /// document on purpose: what a caller may know is that they were refused, and the shape of the
    /// window is not something a public endpoint owes an anonymous caller.
    /// </remarks>
    /// <param name="options">The limiter options the host is building.</param>
    public static void Configure(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
            Recipient(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = TimeSpan.FromSeconds(WindowSeconds),
                // No queue: a visitor waiting on a window they cannot see is worse served than one
                // told plainly that the trainer has had enough mail for the minute.
                QueueLimit = 0
            }));
    }

    /// <summary>
    /// The trainer this request is addressed to, as the partition key.
    /// </summary>
    /// <remarks>
    /// A request the router could not resolve a trainer for falls into one shared partition rather
    /// than into none: an unpartitioned fallback would be a way past the window.
    /// </remarks>
    private static string Recipient(HttpContext context) =>
        context.Request.RouteValues.TryGetValue(TrainerRouteValue, out var trainer)
        && trainer is not null
            ? Convert.ToString(trainer, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
}
