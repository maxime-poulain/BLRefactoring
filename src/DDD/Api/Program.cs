using TrainingHub.DDD.Application.Services.CatalogServices;
using TrainingHub.DDD.Application.Services.OutboxServices;
using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.DDD.Application.Services.TrainingServices;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// The developer's own overrides, appended after every committed source, the user secrets and the
// environment, so they always win. Optional, git-ignored and excluded from the Docker build
// context: in a public repository this file is where a local connection string or a real SMTP
// key is allowed to exist — and the only local channel that reaches dotnet ef. See ADR 0035.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the CQRS host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// One error format for both hosts: RFC 7807 ProblemDetails, whatever failed.
builder.Services.AddApiProblemDetails();

// One logging pipeline for both hosts: Serilog, console and rolling files, tuned by the
// ApiLogging section. Declared in Shared.Api so neither host can quietly log less than the other.
builder.Services.AddApiLogging(builder.Configuration);

// The framework's OpenAPI generator, shared with the CQRS host — which described the same API with
// a different library, and without a security scheme at all. See ADR 0006.
builder.Services.AddApiOpenApi();

// The five readiness probes — database, object store, mail relay, outbox poison, pending
// migrations — shared with the CQRS host so neither can answer less than the other. See ADR 0037
// for the pair of endpoints, ADR 0045 for the schema.
builder.Services.AddApiHealth();

// The dashboard over those probes. Self-gating: outside Development this is a no-op, so the call
// stays unconditional here and production never grows a control room.
builder.Services.AddApiHealthDashboard(builder.Environment);

builder.Services.AddTransient<ITrainingApplicationService, TrainingApplicationService>();
builder.Services.AddTransient<ITrainerApplicationService, TrainerApplicationService>();

// The two application services that drive no aggregate: one reads the search index through the
// query half of that context's published language (ADR 0059), the other reads and requeues the
// platform's own delivery table (ADR 0061).
builder.Services.AddTransient<ICatalogApplicationService, CatalogApplicationService>();
builder.Services.AddTransient<IOutboxApplicationService, OutboxApplicationService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Transient;
    options.Assemblies = [typeof(TrainerDeletedDomainEvent).Assembly, typeof(DeleteTrainingWhenTrainerDeletedEventHandler).Assembly];
});

// Identity, JWT validation and the three authorization policies are the same on both hosts, and
// are declared once in TrainingHub.Shared.Api so neither can quietly lose a rule the other keeps.
builder.Services.AddApiIdentity(builder.Configuration);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddApiAuthorization();

// The window bounding how much mail one trainer can be made to receive, partitioned by the
// recipient because behind the proxy every caller wears the same address (ADR 0082).
builder.Services.AddRateLimiter(ContactRateLimiting.Configure);

var app = builder.Build();

// Configure the HTTP request pipeline.

// First, so everything downstream is covered: authentication, authorization, routing and the
// actions alike. Declared once in Shared.Api and shared with the CQRS host.
app.UseApiExceptionHandling();

// One line per request — the ApiLogging defaults silence the framework's own narration.
app.UseApiLogging();

if (app.Environment.IsDevelopment())
{
    app.UseApiOpenApi();
}

app.UseHttpsRedirection();

app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();

// After authorization, so a refused caller is refused before a window is spent on them.
app.UseRateLimiter();

app.MapControllers();

// /health/live and /health/ready, both anonymous: an orchestrator holds no token, and the body
// names statuses and nothing else. See ADR 0037.
app.MapApiHealth();

// /healthchecks-ui and the detailed endpoint it reads — Development only, like Scalar; the gate
// lives inside the extension.
app.MapApiHealthDashboard();

await app.EnsureDatabasesAreUpToDateAsync();

// After the migrations, because the role is a row: Development only, and a no-op unless a
// developer names their own account in appsettings.Local.json. See ADR 0051 — there is no endpoint
// that grants a role, on either host.
await app.EnsureAdministratorAsync();

// And after the administrator, because the catalog is what an administrator is there to look at.
// Development only, and a no-op unless DevelopmentData:Enabled says otherwise — five hundred
// trainings is not something to discover on a machine that was only supposed to start (ADR 0079).
await app.EnsureDevelopmentCatalogAsync();

app.Run();

/// <summary>
/// The entry point of the layered host.
/// </summary>
/// <remarks>
/// Declared explicitly, rather than left to the compiler, so the integration tests can
/// name it as the type argument to <c>WebApplicationFactory</c>.
/// </remarks>
public sealed partial class Program { }
