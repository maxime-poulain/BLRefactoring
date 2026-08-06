# 0045 — Fail readiness while a migration is pending

- **Status:** Accepted
- **Amends:** [0003](0003-apply-migrations-on-startup-in-development-only.md), [0037](0037-answer-for-the-hosts-health-at-two-endpoints.md)
- **Date:** 2026-08-06

## Context

ADR 0003 stopped the hosts migrating their databases outside Development, and kept a check in place
of the migration:

> **Startup still checks, and says so.** Skipping silently would trade one failure mode for a worse
> one: a host that starts happily and then fails on the first request touching a missing column,
> with nothing in the logs to connect the two.

`EnsureDatabasesAreUpToDateAsync` reads the pending migrations of both contexts and names them —
`LogInformation` when there are none, `LogCritical` when there are. That was the whole of what the
host could do at the time, and the record said so in the alternative it came closest to taking:

> **Fail fast: throw when a migration is pending outside Development.** […] It lost because the
> check also fails when the database is merely unreachable for a moment, which turns a transient
> blip during a rolling restart into an outage […]. **If this service ever gains a readiness probe,
> the right answer is to fail that probe rather than the process, and this record should be
> revisited then.**

[ADR 0037](0037-answer-for-the-hosts-health-at-two-endpoints.md) gave it one. `/health/ready` runs
the probes tagged `ready` and answers names and statuses; an orchestrator polls it to decide whether
this instance should receive traffic. The condition ADR 0003 named has been met, and nothing was
done about it.

What that leaves is a host whose schema is behind, which logs one critical line at startup and then
reports itself **ready**. The orchestrator reads the probe, not the log. Traffic arrives, and the
first request touching a missing column fails — which is exactly the failure ADR 0003 set out to
prevent, arriving through the one channel it had no way to close. `LogCritical` is addressed to a
human who may be reading, at a moment nobody is; readiness is addressed to the machine that is
already asking, every few seconds, and acting on the answer.

## Decision

**A fifth readiness probe answers for the schema.** `PendingMigrationsHealthCheck` reads the pending
migrations of `TrainingContext` and `TrainingIdentityDbContext` and reports `Unhealthy` while either
has any, naming the count per context in its description. It carries the `ready` tag, so
`/health/ready` fails and `/health/live` does not: the process is fine, and restarting it would fix
nothing.

**`Unhealthy`, not `Degraded`.** The outbox's poison gauge is the one probe that declares
`Degraded`, because poison is operator evidence that halts nothing (ADR 0033) and a host that still
serves must not be pulled from rotation over it. A missing column is the opposite kind of fact: not
evidence about past work, but a promise that requests touching those tables will fail. Readiness is
the switch for *do not send me traffic yet*, and this is what it is for.

**The probe re-reads on every poll, and caches nothing.** It exists to serve a deployment whose two
steps can land in either order — the schema applied out of band, the new version started — so the
answer has to change on its own when the missing half arrives. Readiness must go green without a
restart, or it has reintroduced the restart ADR 0003 removed the need for.

**The startup log stays exactly as it is.** The probe says *whether*; the log says *which*. ADR 0037
holds the readiness body to names and statuses on an anonymous route, deliberately, so the migration
names cannot travel that way. An operator who sees `migrations: Unhealthy` needs `Pending:
AddOutboxLease, AddOutboxConsumerLedger` from the log to know what to apply. Neither replaces the
other, and ADR 0003's "startup still checks, and says so" survives intact.

**It is public where its four siblings are internal.** `SqlServerHealthCheck`, `ObjectStoreHealthCheck`,
`SmtpHealthCheck` and `OutboxPoisonHealthCheck` each wrap one call whose whole behaviour belongs to
the dependency it names, and an end-to-end poll against a real container proves them. This one
composes two contexts, an either-side condition and a description — and its failing branch *is* this
decision, the branch no integration test can reach without emptying the migration history that one
SQL Server container shares with every other test in the run. So it is reachable directly instead.
`InternalsVisibleTo` would have bought the same access and cost more: this solution has none, and
argues from that absence twice — in the analyzer ruleset, where a whole category is declined because
CA1515 would demand the plumbing, and in `TrainingTransferDomainServiceTests`, whose claim that the
public path suffices rests on there being no private one. One public type is the narrower change, and
`HealthResponseWriter` is already public in the same folder.

**It runs in every environment, ungated.** In Development the host has just migrated both contexts,
so the probe is green by construction and costs a query nobody notices. A gate would be a second
thing to reason about for no behaviour — and the ungated version has one small virtue: add a
migration while the host runs, and the Development dashboard shows it going red.

## Consequences

- **A stale schema now stops traffic instead of announcing itself.** This is the point, and it is a
  behaviour change in production: an instance that would previously have served and failed per
  request now serves nothing until the schema catches up. That is the correct trade — a failing
  probe is diagnosable from the outside, and a request failing on a missing column is not.
- **Readiness costs a second round trip per poll.** `GetPendingMigrationsAsync` reads
  `__EFMigrationsHistory` and compares it with the migrations in the assembly, once per context. The
  `sql` probe already opens a connection each poll; this adds two cheap reads to a route polled
  every few seconds. Measured against serving a request that fails, it is not close.
- **An unreachable database now fails two probes.** `sql` and `migrations` report the same outage in
  two entries. Accepted rather than deduplicated: they answer different questions, and the reader
  who has to tell "the database is gone" from "the database is old" is better served by two honest
  entries than by one that stays quiet when the other speaks.
- **The fail-fast option ADR 0003 rejected stays rejected.** A transient blip now fails a probe,
  which recovers on the next poll, rather than the process, which crash-loops. That was the whole
  argument against throwing, and moving the check to readiness is what preserves it.
- **The test graph gains a relational provider, and a pin behind it.**
  `Microsoft.EntityFrameworkCore.Sqlite` exists in this solution for one test class and reaches no
  project under `src/` — the hosts speak SQL Server and nothing else. It arrives carrying
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which NuGet's audit reports as a high-severity advisory, so
  `Directory.Packages.props` pins that native package to 2.1.12 the way it already pins
  `Microsoft.OpenApi`. A dependency added for a test is still a dependency, and this is what it cost.
- **The readiness surface grows from four probes to five.** Every sentence that counted four is now
  wrong; `EveryCountedClaim_AgreesWithTheCode` reads `Probes()` from the code and fails until each
  is corrected (ADR 0038).

## Alternatives considered

**Throw at startup when a migration is pending.** The option ADR 0003 weighed and rejected, and this
record does not reopen it: a host that cannot serve correctly arguably should not start, but the
same check fails when the database is briefly unreachable, and a crash-looping instance is harder to
diagnose than a running one that reports itself unready. Readiness gets the same refusal to serve
with none of the cost.

**Report `Degraded` instead of `Unhealthy`.** Would put the entry in the body and leave the instance
in rotation, since only `Unhealthy` fails the endpoint's overall status. That is precisely today's
defect with an extra line of JSON — the host still receives the requests it cannot answer.

**Cache the answer for the lifetime of the process.** Cheaper, and wrong for the sequence this
serves: the schema is applied out of band, and a cached "pending" would hold the instance out of
rotation until somebody restarted it. The restart is the thing ADR 0003 arranged not to need.

**Publish it at a third address, `/health/schema`.** A route nobody polls is a route nobody reads.
The orchestrator already asks one question every few seconds; the answer belongs in it.

**Leave it to the deployment pipeline — apply migrations before starting the new version.** That is
ADR 0003's decision and it remains right; this record does not replace the discipline, it makes the
discipline's failure visible. A pipeline step that was skipped, ran against the wrong database, or
half-succeeded is exactly the case where every other signal is already absent.

## Verification

`Readiness_AnswersForThePendingMigrations` in `HealthRules` pins the registration: the probe type
exists in the shared API seam, implements `IHealthCheck`, and is registered by `AddApiHealth` under
the `ready` tag — proven red first against the seam before the type existed, and again with the tag
removed, which is the shape the rule exists to catch since an untagged check runs on no endpoint at
all.

`PendingMigrationsHealthCheckTests` drives the probe directly, over SQLite in memory: with no
history table it answers `Unhealthy` and names both contexts and their counts, and once every
migration is stamped into the history through EF's own `IHistoryRepository` the same probe over the
same connection answers `Healthy` — which is the no-caching claim above, executable. The schema is
never built there: the migrations are scaffolded for SQL Server, and `GetPendingMigrationsAsync`
compares the assembly's migrations with the history rows, which is the whole of what this probe
asks. Both tests were watched failing against a probe whose condition had been inverted.

`HealthTest` in the shared TestKit — so both API suites run it — asserts that `/health/ready` names
five checks including `migrations` and that every one of them is `Healthy` on a host whose
Development startup has just migrated. The dashboard fact reads the same five.
