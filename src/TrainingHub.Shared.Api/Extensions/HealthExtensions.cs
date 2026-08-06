using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using TrainingHub.Shared.Api.Health;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// How an API host answers for its health: liveness at one address, readiness at another, and a
/// dashboard over both in Development.
/// </summary>
/// <remarks>
/// Shared by both stacks for the reason the logging pair is: an endpoint mapped in one host's
/// <c>Program.cs</c> only answers for that host, and the other one silently answers 404 to the
/// same poll. The serving surface — the two endpoints and the five probes — ships entirely in the
/// ASP.NET Core shared framework and costs no package; the Development dashboard is what the
/// three <c>AspNetCore.HealthChecks.UI</c> packages buy. The probes run per request and never
/// eagerly: no <c>IHealthCheckPublisher</c> is registered and no probe constructor performs IO,
/// so the design-time boots construct the host without a single probe firing — the dashboard's
/// collector is a hosted service, and a design-time boot never starts those. See ADR 0037.
/// </remarks>
public static class HealthExtensions
{
    // The tag MapApiHealth selects the readiness probes by. A check registered without it — none
    // exists today — would run on no endpoint at all, which is the safe default for a mistake.
    private const string ReadyTag = "ready";

    /// <summary>
    /// Registers the five readiness probes: the database, the object store, the mail relay, the
    /// outbox's poison gauge, and the schema.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The poison gauge alone declares <see cref="HealthStatus.Degraded"/> as its failure
    /// status: poison is operator evidence that halts nothing (ADR 0033), and a host that still
    /// serves must not fail readiness over it. The other four answer for something the serving
    /// path genuinely needs — three dependencies, and a schema without which requests touching
    /// the affected tables will fail (ADR 0045) — so their failures keep the default
    /// <see cref="HealthStatus.Unhealthy"/>.
    /// </remarks>
    public static IServiceCollection AddApiHealth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sql", tags: [ReadyTag])
            .AddCheck<ObjectStoreHealthCheck>("s3", tags: [ReadyTag])
            .AddCheck<SmtpHealthCheck>("smtp", tags: [ReadyTag])
            .AddCheck<OutboxPoisonHealthCheck>(
                "outbox", failureStatus: HealthStatus.Degraded, tags: [ReadyTag])
            .AddCheck<PendingMigrationsHealthCheck>("migrations", tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Maps <c>/health/live</c> and <c>/health/ready</c>, both anonymous.
    /// </summary>
    /// <param name="app">The application being composed.</param>
    /// <returns>The application, for chaining.</returns>
    /// <remarks>
    /// Liveness runs no checks at all — the predicate refuses every registration — because a 200
    /// there means "the process is up and routing", which is all a container restart decision
    /// should ever read: restarting this process cannot fix a dependency. Readiness runs the five
    /// tagged probes and answers through <see cref="HealthResponseWriter"/>, names and statuses
    /// only. Anonymous on purpose: an orchestrator holds no token, and the body carries nothing
    /// worth a token (ADR 0037).
    /// </remarks>
    public static WebApplication MapApiHealth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        return app;
    }

    /// <summary>
    /// Registers the Development dashboard: the UI page, its polling collector, and the
    /// in-memory store its history lives in.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host's environment — the gate lives here, not at the call site.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// Self-gating on purpose, unlike the OpenAPI pair whose gate sits in each <c>Program.cs</c>:
    /// the architecture rule pins these calls by literal, and a call site inside an if-branch is
    /// exactly the shape a scan misreads. Outside Development this is a no-op, so production
    /// never grows a control room. In-memory storage deliberately — a dashboard that forgets on
    /// restart is a feature in development, the same argument Mailpit's missing volume makes.
    /// The collector polls this host's own <c>/health/ui</c> every ten seconds; it is a hosted
    /// service, so the design-time boots — which never call <c>Run()</c> — still probe nothing
    /// (ADR 0037).
    /// </remarks>
    public static IServiceCollection AddApiHealthDashboard(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
        {
            return services;
        }

        services
            .AddHealthChecksUI(setup =>
            {
                setup.SetEvaluationTimeInSeconds(10);
                setup.AddHealthCheckEndpoint(environment.ApplicationName, "/health/ui");
            })
            .AddInMemoryStorage();

        return services;
    }

    /// <summary>
    /// Maps the dashboard at <c>/healthchecks-ui</c> and the detailed endpoint it reads, in
    /// Development only.
    /// </summary>
    /// <param name="app">The application being composed.</param>
    /// <returns>The application, for chaining.</returns>
    /// <remarks>
    /// <c>/health/ui</c> answers the same five probes as <c>/health/ready</c> but in the UI's own
    /// format — which carries descriptions, durations and exception messages. That is exactly
    /// what ADR 0037 keeps off the anonymous production surface, and why this endpoint exists
    /// only where Scalar's reference UI does: <c>/health/ready</c> and its names-and-statuses
    /// writer stay untouched in every environment, and this one is a Development tool beside the
    /// document UI.
    /// </remarks>
    public static WebApplication MapApiHealthDashboard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapHealthChecks("/health/ui", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecksUI();

        return app;
    }
}
