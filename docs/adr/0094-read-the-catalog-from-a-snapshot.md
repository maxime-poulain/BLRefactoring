# 0094 — Read the catalog from a snapshot

- **Status:** Accepted
- **Date:** 2026-08-19

## Context

The integration job failed on one test out of two hundred and twenty, on the CQRS host, with a
status nobody had written a line to produce:

> Expected page.StatusCode to be HttpStatusCode.OK, but found HttpStatusCode.InternalServerError

The host's own log names what happened, and it is not a defect in any line of C# this repository
holds:

```
Unhandled exception while handling GET /Catalog/trainings?term=erased&page=1&pageSize=50
Microsoft.Data.SqlClient.SqlException: Transaction (Process ID 51) was deadlocked on lock resources
  with another process and has been chosen as the deadlock victim. Rerun the transaction.
  at TrainingHub.Shared.Infrastructure.Pagination.PagedQueryableExtensions.ToPagedResultAsync
  at TrainingHub.Shared.Infrastructure.Search.TrainingSearchQuery.SearchAsync
Error Number:1205,State:51,Class:13
```

**The anonymous catalog read was killed by the database.** Beside it, the erasure cascade — carried
by the outbox, the moment a trainer erases their account — was deleting that trainer's rows from
`TrainingSearchEntry` and its topics. Under the server's default `READ COMMITTED`, a reader takes
shared locks as it goes; the deleter takes exclusive ones; the two touch the same two tables in
different orders; and when they meet head to head SQL Server breaks the cycle by killing the cheaper
transaction, which is the read.

**The test is where it became visible, not what is wrong with it.** It polls the catalog until the
erased training has left, so it spends fifteen seconds reading exactly while the cascade writes —
the narrow window the race needs. What the race means outside the suite is worse: a visitor
browsing a public page can be handed a 500 because somebody they have never heard of erased their
account at that moment. Nothing about that is the visitor's business.

It is rare. Thirty integration runs hold one occurrence, and the only other failure in that window
was a different cause. Rare and real is the combination that argues for fixing the property rather
than the symptom: it will not reproduce on demand, and it will come back.

## Decision

**The database reads from a snapshot.** A migration turns `READ_COMMITTED_SNAPSHOT` on, so a reader
under `READ COMMITTED` no longer takes shared locks at all — it reads the committed row version as
of the statement's start. A transaction that takes no shared locks cannot be one side of a
reader/writer lock cycle, so this class of deadlock stops being possible rather than becoming rarer.

It is also what the catalog already claims to be. ADR 0059 made the search index a projection and
declared it eventually consistent by decision: it is rebuilt from facts, read anonymously, and never
the source of truth. A projection's reader has no business blocking the writer that rebuilds it, or
being blocked by it. The lock behavior now matches the design that was already written down.

Three details of the statement are load-bearing, and the migration says so beside them:

- **`suppressTransaction: true`** — EF wraps a migration in a transaction, and `ALTER DATABASE`
  cannot run inside one.
- **`WITH ROLLBACK IMMEDIATE`** — the setting needs exclusive access to the database. Without it the
  statement waits behind every other session, indefinitely.
- **A guard around it** — a database already reading from a snapshot has no sessions worth
  terminating, and a migration that is a no-op when the work is done is one an operator can run
  twice.

## Consequences

- **The catalog's read can no longer be a deadlock victim**, and neither can any other read this
  application makes. Writer-against-writer deadlocks remain possible; nothing here claims otherwise.
- **Applying this to a database that already exists terminates its sessions.** That is what
  `WITH ROLLBACK IMMEDIATE` means, and it is why this is a migration an operator applies out of band
  under ADR 0003 rather than something a host does to a production database on startup.
- **Row versions live in `tempdb`.** RCSI is not free: every updated row carries a version chain
  until no reader needs it, and `tempdb` is where they go. That is the price, and a catalog nobody
  can turn into a 500 by erasing their own account is worth it.
- **A read no longer waits behind a write.** Reads become statement-level snapshots, so a query that
  used to block until a writer committed now answers with what was committed when it started.
  Nothing in this repository depended on that blocking; the one place that reasons about
  concurrency at all is the outbox, and it asks for its locks by name —
  `FROM OutboxMessage WITH (UPDLOCK, READPAST, ROWLOCK)` — which RCSI does not touch.
- **The unit suites see none of this.** They run on SQLite and the in-memory provider, where the
  setting does not exist. This is a fact about a real SQL Server, so its proof is an integration
  fact, and it runs on both hosts.

## Alternatives considered

**Retrying the deadlock.** `EnableRetryOnFailure()` swaps `SqlServerExecutionStrategy` for the
retrying one, whose transient list includes 1205, and the read would succeed on the second attempt.
The cost is not the retry: the retrying strategy refuses user-initiated transactions, and both the
outbox processor and the erasure cascade open their own, so every one of them would have to be
wrapped in `strategy.ExecuteAsync`. That is a wide change to the write path in exchange for making a
read fail and then recover, where the snapshot makes it not fail.

**Re-running the job.** The cheapest thing available, and the one that leaves a public page able to
answer 500 for a reason its visitor cannot see or avoid. A test that fails once in thirty runs is
not a flaky test here; it is a rare event the suite was lucky enough to catch.

**Isolating only the catalog's query.** EF has no per-query isolation level, so this means an
explicit transaction opened around the read at snapshot isolation — which needs
`ALLOW_SNAPSHOT_ISOLATION` on the database anyway, and puts a transaction into a query path that
had none. The same database setting, bought with more code and a narrower guarantee.

## Verification

The rule was seen red twice before the migration was written, once per clause:

> 0 migrations turn READ_COMMITTED_SNAPSHOT on, and ADR 0094 asks for exactly one

and, with a `READ_COMMITTED_SNAPSHOT OFF` planted in the search query:

> `TrainingSearchQuery.cs` turns READ_COMMITTED_SNAPSHOT off

**The migration's first CI run failed loudly, and the refusal was information.** The integration
harness handed its hosts the Testcontainers connection string unchanged, and that string names
`master` — so the suites had been migrating the application schema into a system database all
along, with nothing saying so, and `READ_COMMITTED_SNAPSHOT` cannot even be turned on there. The
statement's `ALTER DATABASE CURRENT` is what finally objected. The harness now rewrites the
consumed connection string to a database of the suite's own, which the hosts migrate into
existence on startup exactly as they would a real one; the container's readiness probe still runs
against `master` and is untouched.

`TheMigratedDatabase_ReadsFromASnapshot` then asks the server itself —
`SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()` — against the
database the host migrated, on both hosts.

**The deadlock itself stays unproven, and this record says so rather than pretending otherwise.** A
race that surfaced once in thirty runs cannot be made to fail on demand, and a test that tried would
be a coin flip wearing an assertion. What is proven is the property that makes the cycle impossible.
