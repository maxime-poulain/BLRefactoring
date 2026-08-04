using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using TrainingHub.Shared.Api.Logging;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Logging;

/// <summary>
/// Behaviour covered for <c>UserIdentityEnricher</c>: which identity each kind of caller gets.
/// </summary>
/// <remarks>
/// The mapping alone — the wiring that puts the enricher into the pipeline, and the template that
/// makes the property visible, are held by <c>LoggingTest</c> over the wire in both suites.
/// </remarks>
public sealed class UserIdentityEnricherTests
{
    /// <summary>
    /// A signed caller, is named by the username.
    /// </summary>
    [Fact]
    public void ASignedCaller_IsNamedByTheUsername()
    {
        var enriched = Enrich(Authenticated(
            new Claim(ClaimTypes.Name, "ada.lovelace"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())));

        enriched.Should().Be("ada.lovelace");
    }

    /// <summary>
    /// A token without a name, falls back to the subject claim.
    /// </summary>
    [Fact]
    public void ATokenWithoutAName_FallsBackToTheSubjectClaim()
    {
        var subject = Guid.NewGuid().ToString();

        var enriched = Enrich(Authenticated(new Claim(ClaimTypes.NameIdentifier, subject)));

        enriched.Should().Be(subject);
    }

    /// <summary>
    /// An unsigned caller, is anonymous.
    /// </summary>
    [Fact]
    public void AnUnsignedCaller_IsAnonymous()
    {
        var enriched = Enrich(new DefaultHttpContext());

        enriched.Should().Be(UserIdentityEnricher.AnonymousIdentity);
    }

    /// <summary>
    /// No request at all, is system.
    /// </summary>
    [Fact]
    public void NoRequestAtAll_IsSystem()
    {
        var enriched = Enrich(httpContext: null);

        enriched.Should().Be(UserIdentityEnricher.SystemIdentity);
    }

    /// <summary>
    /// A property already on the event, is left alone.
    /// </summary>
    /// <remarks>
    /// <c>AddPropertyIfAbsent</c> is the contract every well-behaved enricher keeps: a writer who
    /// deliberately set the property knows something ambient inference does not.
    /// </remarks>
    [Fact]
    public void APropertyAlreadyOnTheEvent_IsLeftAlone()
    {
        var logEvent = EmptyEvent();
        logEvent.AddPropertyIfAbsent(new LogEventProperty(
            UserIdentityEnricher.PropertyName, new ScalarValue("deliberate")));

        new UserIdentityEnricher(new HttpContextAccessor { HttpContext = null })
            .Enrich(logEvent, new ScalarPropertyFactory());

        logEvent.Properties[UserIdentityEnricher.PropertyName]
            .Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("deliberate");
    }

    private static string Enrich(HttpContext? httpContext)
    {
        var logEvent = EmptyEvent();

        new UserIdentityEnricher(new HttpContextAccessor { HttpContext = httpContext })
            .Enrich(logEvent, new ScalarPropertyFactory());

        return logEvent.Properties[UserIdentityEnricher.PropertyName]
            .Should().BeOfType<ScalarValue>().Subject
            .Value.Should().BeOfType<string>().Subject;
    }

    /// <summary>A principal whose identity answers true to IsAuthenticated.</summary>
    /// <remarks>
    /// The authentication type is what flips <c>IsAuthenticated</c>: a <c>ClaimsIdentity</c> built
    /// without one models exactly the anonymous principal ASP.NET Core puts on an unsigned
    /// request, which is why <see cref="AnUnsignedCaller_IsAnonymous"/> needs no set-up at all.
    /// </remarks>
    private static DefaultHttpContext Authenticated(params Claim[] claims) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
    };

    private static LogEvent EmptyEvent() => new(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        exception: null,
        new MessageTemplate([]),
        properties: []);

    private sealed class ScalarPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
