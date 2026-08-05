# 0037 — Answer for the host's health at two endpoints

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Nothing in this system answers the question "are you well?". The compose file gates start-up on
the *dependencies'* health — SQL Server, SeaweedFS and Mailpit each carry a `healthcheck:` block,
two of them with battle scars in their comments — while the one first-party container is the only
service with no check at all. An orchestrator, a load balancer, or a developer with `curl` has no
way to ask either API host whether it serves, let alone whether the world it depends on is
reachable. And the outbox's poison — deliberate operator evidence since ADR 0033 — announces
itself exactly once, in a log line an operator has to have been watching.

## Decision

**Every host answers for its own health: liveness says the process serves, readiness says its
world — database, object store, mail relay, outbox — is reachable, and the body names statuses
and nothing else.**

- **Two endpoints, anonymous.** `/health/live` runs no checks: a 200 means the process is up and
  routing, which is all a container restart decision should ever read. `/health/ready` runs the
  four probes and answers a hand-rolled JSON body of check names and statuses — no descriptions,
  no exception messages, no durations, so there is nothing in it worth protecting and nothing an
  attacker learns beyond what connecting to the port already told them. Anonymous on purpose: an
  orchestrator holds no token, and the repository sets no fallback authorization policy that
  would need opting out of.
- **Wired once, in the shared API layer.** `AddApiHealth` / `MapApiHealth` join the
  `AddApiLogging` family in `Shared.Api`, so neither API host can answer less than the other.
  The Blazor BFF cannot consume that extension — the TFM split keeps the net10 shared assemblies
  out of its net9 reach — so it carries two inline framework lines for liveness only; its world
  is the API, and proxying a readiness answer would be a decision of its own.
- **The probes are hand-rolled against clients the solution already owns.** SQL Server is
  `TrainingContext.Database.CanConnectAsync`. The object store is one *signed*
  `ListObjectsV2(MaxKeys: 1)` — a real read that proves endpoint, credentials and bucket in one
  round-trip, where a metadata stub is exactly the kind of call an S3-compatible server is
  entitled to fake. The mail relay is a MailKit connect-and-quit that never authenticates:
  reachability is the question, and spending an authentication per poll against a rate-limiting
  relay is a self-inflicted outage. The library-confinement rules shape where this code lives:
  MailKit and the AWS SDK may only be named by the infrastructure, so each probe's IO sits behind
  a small reachability port there, and the health checks in `Shared.Api` consume the ports.
- **Poison degrades; it does not fail.** The fourth check counts the outbox rows whose attempts
  exhausted the budget and answers Degraded when any exist. ADR 0033 made poison operator
  evidence that halts nothing — the host still serves, so failing readiness over it would take
  traffic away from a process that is fine. The dead-letter surface with a URL — a list, a
  requeue — remains deferred exactly as 0025 and 0033 left it; this gauge is the pollable half of
  0033's log line, one notch further, still not that endpoint.
- **Checks run per request, never eagerly.** No `IHealthCheckPublisher` is registered and no
  probe constructor performs IO, so the design-time boots — `dotnet ef`, the OpenAPI document
  tool — construct the host without a single probe firing, the same discipline the bucket
  bootstrapper and the migration runner already keep. The one exception is deliberate and
  Development-only: the dashboard's collector below re-probes on a ten-second cadence — and it is
  a hosted service, which a design-time boot never starts, so the discipline holds exactly where
  it was written to.
- **The dashboard is a Development tool.** `/healthchecks-ui` serves the Xabaril UI over the same
  four probes, on the same bargain as Scalar's reference UI: a developer watching the stack wants
  a page, and production does not need to publish a control room to be observable. The pair —
  `AddApiHealthDashboard` / `MapApiHealthDashboard` — is self-gating rather than gated at the
  call site, because the rule that pins the calls scans source by literal and an if-branch is
  exactly the shape a scan misreads: both hosts call it unconditionally, and outside Development
  it is a no-op. The page reads its own host's `/health/ui`, which answers in the UI's format —
  descriptions, durations, exception messages included. That richer body is precisely what this
  record keeps off the anonymous production surface, which is why the endpoint exists only where
  the page that reads it does; `/health/ready` and its names-and-statuses writer are untouched in
  every environment. The history lives in the in-memory store and forgets on restart —
  deliberately, the same argument Mailpit's missing volume makes.

## Consequences

- The integration suites prove `/health/ready` genuinely green: they run real SQL Server,
  SeaweedFS and Mailpit containers, so the readiness fact is an end-to-end probe of all four
  checks, not a mock's opinion. The response schema assertion — each entry carries exactly a name
  and a status — is the no-secrets claim, executable.
- The `ddd-api` container joins the three dependencies that already answer: `curl` enters the
  final image stage for exactly that healthcheck line, polling `/health/live`. Liveness, not
  readiness, deliberately — a container check drives restart decisions, and restarting this
  process cannot fix a dependency. CI never builds that image, so the check is proven by running
  the stack, not by a pipeline.
- The serving surface costs no package: `AddHealthChecks`, `MapHealthChecks` and `IHealthCheck`
  all ship in the ASP.NET Core shared framework the API projects already reference. What was paid
  for is the Development dashboard alone — the three `AspNetCore.HealthChecks.UI` packages, plus
  two transitive pins the graph forced: the EF `InMemory` provider (the storage package asks for
  the EF 8 build, which throws `MissingMethodException` against an EF 10 runtime) and
  `KubernetesClient` (the UI pulls a vulnerable 15.0.1 for a discovery feature this repository
  never turns on — GHSA-w7r3-mgwf-4mqq, closed by pinning 17.0.14, the `Microsoft.OpenApi`
  mechanism again). The health endpoints are invisible to the OpenAPI document and to the
  host-parity rules — they are not controller actions — so the generated client does not change.

## Alternatives considered

**The community health-check *probe* packages (`AspNetCore.HealthChecks.SqlServer` and
siblings).** Off-the-shelf probes for SQL Server, S3 and SMTP exist, from the same family the
dashboard comes from. Still rejected: each probe here is some fifteen lines against a client the
solution already owns and configures, and a probe is the part of this decision that carries
judgement — which call proves reachability, what degrades rather than fails. The UI was taken
and the probes were not, because a dashboard is a product worth buying and a `CanConnectAsync`
is not.

**`AddDbContextCheck` (the EF Core health package).** One package for one line —
`CanConnectAsync` behind an extension method. The line is written by hand instead.

**Serving the dashboard everywhere, behind authorization.** The UI in production is a control
room: it needs an owner, an authentication story for a page orchestrators never read, and an
answer for why its detailed endpoint may say what `/health/ready` deliberately does not.
Development-only keeps the no-secrets claim absolute where it matters and the page where its one
audience — a developer watching the stack — actually is. The first version of this record
rejected the UI outright on that argument; what changed is the scope, not the argument.

**An eager publisher (`IHealthCheckPublisher`).** Background re-probing with nobody consuming
the result. The dashboard's collector is the same mechanism with a consumer — a page — which is
why it exists exactly where the page does and nowhere else; a publisher on the production hosts
would re-probe for an audience that polls the endpoints anyway.

**Authorization on the health endpoints.** The consumers are orchestrators and probes that hold
no token, and the statuses-only body means there is nothing to protect. Requiring auth would
break every caller to guard nothing.

## Verification

`HealthTest` in the shared TestKit proves both endpoints on both API hosts against the real
containers: `/health/live` answers an anonymous caller `Healthy`, and `/health/ready` names
exactly the four checks — sql, s3, smtp, outbox — each entry carrying a name, a status and
nothing else. The BFF suite proves its inline liveness the same way. A unit test feeds the
response writer a report loaded with exceptions and descriptions and holds the JSON to names and
statuses only. Two further facts hold the dashboard: the page answers at `/healthchecks-ui` and
`/health/ui` speaks the UI's format over exactly the four probes — both running because the
TestKit hosts in Development, the only environment the dashboard exists in. Four rules defend
the wiring — `BothApiHosts_AnswerForTheirHealth` pins `AddApiHealth`/`MapApiHealth` in both API
composition roots, `TheBff_AnswersForItsLiveness` pins the inline pair in the BFF's,
`BothApiHosts_ServeTheDashboardInDevelopment` pins the self-gating dashboard pair, and
`OnlyTheHealthSeam_TouchesTheDashboardLibrary` confines the UI library to the seam that adopts
it — each proven red first: the endpoint rules against all three hosts before a single
`Program.cs` changed, the dashboard rule before its pair existed, the confinement rule against a
probe type naming the library outside the seam.
