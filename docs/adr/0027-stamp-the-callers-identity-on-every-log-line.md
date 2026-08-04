# 0027 — Stamp the caller's identity on every log line

- **Status:** Accepted
- **Date:** 2026-08-04

## Context

ADR 0026 put the hosts' logs on disk; the next question a log file gets asked is *who did this*.
The ask was concrete: every line written while an authenticated request runs should carry the
caller — the username, falling back to the user id when a token carries no name — with anonymous
requests marked explicitly, no leakage between requests, and nothing for the writer of a log
statement to remember. What was open was the mechanism: Serilog offers at least three, and the
obvious one is not the right one here.

## Decision

**A write-time enricher, `UserIdentityEnricher`, reading the caller through
`IHttpContextAccessor`.** It stamps a `User` property on every event: the `Name` claim of the
signed-in principal, else the subject claim, `Anonymous` for a request nobody signed, and `System`
for a line written outside any request — the outbox worker and the start-up narration are somebody
too, and a blank would read as an accident.

**Evaluated per event, never cached per request.** The same request is anonymous until the
authentication middleware has run; whatever is known when a line is written is what the line says.
That is also what makes the mechanism leak-proof: nothing is stored, so there is nothing to leak —
the accessor's `AsyncLocal` scoping does the isolation the requirements demanded.

**Visible by template, not by convention.** A text sink shows no property the template does not
name, so the shared output template renders the caller on every line — `[ada.lovelace]`,
`[Anonymous]`, `[System]` — in the console and the files alike. The enricher keeps
`AddPropertyIfAbsent` semantics: a writer who deliberately set `User` knows something ambient
inference does not.

**Wired inside `AddApiLogging`, nowhere else.** No host changed, so the host-symmetry rule from
ADR 0026 keeps covering the whole pipeline, this decision included.

## Consequences

- Every line answers *who*: signed requests carry the username end to end — the per-request
  summary, the EF command logs beneath it, anything a handler writes.
- A username in a log file is personal data. Retention was already bounded by ADR 0026; the
  integration suites now point the file sink at a per-fixture temporary directory deleted with the
  fixture, so test data does not outlive the run — which also gave the file sink its first
  end-to-end coverage.
- The claim lookup runs once per event. It is two dictionary probes on an `AsyncLocal`; if a
  profiler ever says otherwise, caching per request is the wrong fix — see above — and batching
  sinks are the right one.
- `IHttpContextAccessor` appears in one more place. It is the documented tool for exactly this:
  ambient request context consumed by cross-cutting infrastructure that must not take a
  per-request dependency.

## Alternatives considered

**A middleware pushing `LogContext.PushProperty`.** The idiomatic reflex, and the pipeline's own
ordering defeats it twice. The identity exists only after `UseAuthentication`, so the push would
need a wiring point behind it — a third extension call in every host, and a wider surface for the
symmetry rule to hold. And the request-logging middleware sits near the front, writing its
completion line as the stack unwinds — after an inner middleware's scope has been disposed — so
the one line per request most worth stamping would be precisely the one that misses the property.

**`ILogger.BeginScope` in a middleware.** The same disposal-ordering problem in
`Microsoft.Extensions.Logging` vocabulary, with an extra cost: scope properties only reach events
written through `ILogger`, while the enricher covers everything the pipeline emits, whoever wrote
it.

**`IDiagnosticContext.Set` in the request-logging options.** Built for exactly one event — the
completion line — and only that event. It answers a different question ("summarise this request")
and would leave every other line during the request unstamped.

**Caching the identity on `HttpContext.Items`.** Saves a claim probe per event and buys a bug: an
event written before authentication would freeze `Anonymous` into the rest of the request, or the
reverse. Correctness first; the probe is trivial.
