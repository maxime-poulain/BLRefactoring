using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace TrainingHub.Shared.Api.Logging;

/// <summary>
/// Stamps every log event with the identity behind the current HTTP request.
/// </summary>
/// <remarks>
/// An enricher evaluated when each event is written, not a middleware pushing
/// <c>LogContext</c>, and the pipeline's own ordering is the reason. The identity only exists
/// after the authentication middleware has run, yet the request-logging middleware sits near the
/// front and writes its completion line as the stack unwinds — after any inner middleware's
/// <c>LogContext</c> scope has already been disposed. A pushed property would therefore need a
/// second wiring point behind <c>UseAuthentication</c> in every host, and would still miss the
/// one line per request most worth stamping. Reading the caller at write time answers both:
/// whatever is known when a line is written is what the line says, wherever the writer sits in
/// the pipeline. Outside a request there is no caller, and the property says <c>System</c> rather
/// than staying blank — the outbox worker's lines and the start-up narration are somebody too.
/// </remarks>
/// <param name="httpContextAccessor">The ambient request, absent outside one.</param>
public sealed class UserIdentityEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    /// <summary>
    /// The property this enricher writes, as the output templates spell it.
    /// </summary>
    public const string PropertyName = "User";

    /// <summary>The identity of a line written outside any HTTP request.</summary>
    public const string SystemIdentity = "System";

    /// <summary>The identity of a request nobody has signed.</summary>
    public const string AnonymousIdentity = "Anonymous";

    /// <summary>
    /// Adds the caller's identity to the event.
    /// </summary>
    /// <param name="logEvent">The event being written.</param>
    /// <param name="propertyFactory">The factory the pipeline hands in.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, CurrentCaller()));
    }

    private string CurrentCaller()
    {
        // Read on every event, never cached on the request: the same request is anonymous until
        // the authentication middleware has run, and a cached answer would freeze whichever state
        // the first written line happened to see.
        var user = httpContextAccessor.HttpContext?.User;

        if (user is null)
        {
            return SystemIdentity;
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return AnonymousIdentity;
        }

        // The username first — it is what a reader recognises — and the subject claim when a
        // token carries no name. Both claims are written by TokenService, so the last arm is a
        // token minted by something else entirely; saying so beats a blank.
        return user.Identity.Name
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "Authenticated";
    }
}
