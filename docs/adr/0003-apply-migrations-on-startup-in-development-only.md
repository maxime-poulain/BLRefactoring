# 0003 — Apply migrations on startup in Development only

- **Status:** Accepted — amended by [0045](0045-fail-readiness-while-a-migration-is-pending.md): the readiness probe this record said to revisit it for now exists, and a pending migration fails it
- **Date:** 2026-08-01

## Context

Both API hosts ended their `Program.cs` with `await app.MigrateDatabasesAsync()`, unconditionally.
Every start of either host applied the pending migrations of `TrainingContext` and
`TrainingIdentityDbContext`, in every environment.

It is an excellent development loop. `docker compose up` for SQL Server, `dotnet run`, and the
database exists at the right version with nothing else to remember. The integration tests get their
schema the same way: `ApiFactory` starts a real host against a Testcontainers instance, and that
startup is what creates the tables Respawn then reads to build its checkpoint.

Outside Development the same line is a liability, for reasons that have nothing to do with
convenience:

- **Concurrency.** Two instances starting together — a rolling deploy, a scaled-out service, a
  restarted pod — run `Migrate()` against the same database at the same time. EF Core takes no lock
  that makes this safe.
- **Privilege.** The application must hold DDL rights on its own schema permanently, so an
  application-level compromise becomes a schema-level one.
- **Reversibility.** A schema change applied as a side effect of a process starting cannot be undone
  by stopping that process. The deployment and the migration become one event that can only half
  succeed, and the half that succeeded is the irreversible one.
- **Visibility.** Nobody decided to run it. It appears in no release plan and in no change log.

## Decision

**Migrations are applied on startup in Development, and only there.**

In every other environment the host applies nothing. The schema is brought up to date out of band —
`dotnet ef database update`, or a migration bundle — as a deliberate, reversible step of the
release, performed before the new version starts serving.

**Startup still checks, and says so.** Skipping silently would trade one failure mode for a worse
one: a host that starts happily and then fails on the first request touching a missing column, with
nothing in the logs to connect the two. So outside Development the host reads the pending migrations
of both contexts and logs them by name — `LogInformation` when there are none, `LogCritical` when
there are, naming each one and stating that this host will not apply them.

`MigrateDatabasesAsync` is renamed `EnsureDatabasesAreUpToDateAsync`, because it no longer always
migrates and a name that claimed otherwise would be the next thing to mislead someone.

## Consequences

- Concurrent starts can no longer race on DDL, and the application no longer needs standing DDL
  rights outside Development.
- A schema change becomes a visible step of a release, which can be reviewed, scheduled and rolled
  back independently of the deployment that needs it.
- A misconfigured environment is loud at startup rather than silent until the first affected
  request.
- **The development loop is unchanged**, which was a requirement rather than a happy accident: the
  integration tests depend on it. `ApiFactory` calls `builder.UseEnvironment("Development")`, so the
  test host still migrates, and the suite needed no adjustment.

Against that:

- **Deploying now has a prerequisite.** Ship a version whose migration has not been applied and the
  affected endpoints fail. The log makes the cause obvious; it does not make the failure not happen.
- The release pipeline gains a step it did not have. This repository has no deployment workflow at
  all, so the ADR states the obligation without a script to point at — an honest gap rather than a
  hidden one.
- Two extra round trips at startup, one per context, to read the migration history. Negligible, and
  worth naming so nobody wonders later.

## Alternatives considered

**Keep migrating on startup everywhere.** The status quo. Convenient in exactly one environment and
wrong in all the others, for the four reasons above.

**Fail fast: throw when a migration is pending outside Development.** Genuinely tempting, and it was
the closest call here. A host that cannot serve correctly arguably should not start. It lost because
the check also fails when the database is merely unreachable for a moment, which turns a transient
blip during a rolling restart into an outage, and because a crash-looping instance is harder to
diagnose than a running one printing a critical log line naming the missing migration. If this
service ever gains a readiness probe, the right answer is to fail that probe rather than the
process, and this record should be revisited then.

**Migrate from a separate job or init container.** The standard production answer, and compatible
with this decision rather than an alternative to it: it is one way to perform the out-of-band step.
Not chosen *here* only because the repository has no deployment target to configure it on.

**A dedicated migrator console application in the solution.** Would make the step explicit and
runnable, at the cost of another deployable and another `Program.cs` to keep in step with two
contexts. `dotnet ef` and migration bundles already do this, from the same source of truth.

**Gate the call in each `Program.cs` instead of inside the extension.** Rejected for the reason the
extension exists at all: a rule written twice is a rule one host can lose. The guard lives with the
thing it guards.
