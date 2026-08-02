# 0002 — Keep domain reactions in the transaction, deliver integration events through an outbox

- **Status:** Accepted — not yet implemented
- **Date:** 2026-08-01

## Context

Aggregates raise domain events; `DomainEventInterceptor` collects them from the tracked entities
during `SavingChangesAsync` and dispatches them **before** anything is written, draining in rounds
until no aggregate holds a pending event. Handlers that stage further changes therefore take part in
the same implicit transaction, and one commit persists the whole outcome. That property is the
reason the design was chosen, and it is genuinely valuable: deleting a trainer and the trainings
that follow is atomic without a single explicit transaction.

The problem is that it applies to *every* handler, including the ones that leave the process. Two
consequences follow, and neither was written down anywhere.

**A side effect can happen for a transaction that never commits.**
`SendWelcomeEmailWhenTrainerCreatedEventHandler` sends the email, and the save that triggered it can
still fail afterwards — an optimistic concurrency conflict, a unique index violation, a lost
connection. The trainer does not exist and the welcome email is gone. The same holds for the search
index, which can end up holding a training the database never accepted.

**Nothing is replayable.** `AggregateRootTypeConfiguration` declares
`builder.Ignore(nameof(IHasDomainEvents.DomainEvents))`: events live on the aggregate in memory and
are never persisted. A process that dies mid-dispatch loses whatever had not run, and no record
exists that it was ever owed.

Both defects share a cause: an in-memory dispatch and a database transaction are being asked to
succeed or fail together, which they cannot do.

## Decision

**Split the two kinds of reaction, and treat them differently.**

**Domain reactions stay exactly as they are** — in-process, dispatched before persistence, part of
the transaction. A handler qualifies when it does nothing but touch the change tracker, because then
its work commits or rolls back with everything else, by construction.

**Integration events go through a transactional outbox.** A handler qualifies when it crosses a
process boundary — sends mail, writes to a search engine, calls another service. Instead of acting,
an application-layer handler translates the domain event into an integration event and writes it to
an outbox table **in the same `TrainingContext`, during the same `SaveChanges`**. The message is
therefore committed atomically with the state change that justified it: both land, or neither does.
This is the mechanism the cascade delete already relies on, so it needs no explicit transaction and
no new machinery in the interceptor.

A background worker then reads the outbox and performs the side effect, after the commit.

### The line, drawn on the handlers that exist

| Handler | Depends on | Side |
|---|---|---|
| `DeleteTrainingWhenTrainerDeletedEventHandler` | `ITrainingRepository` | **domain** — stages deletions into the same `SaveChanges` |
| `AuditWhenTrainerNameChangedEventHandler` | `ILogger` | **domain** — a log line is not another system |
| `SendWelcomeEmailWhenTrainerCreatedEventHandler` | `IEmailSender` | **integration** |
| `NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandler` | `IEmailSender` | **integration** |
| `IndexTrainingWhenTrainingCreatedEventHandler` | `ITrainingSearchIndexer` | **integration** |
| `ReindexTrainingWhenTrainingEditedEventHandler` | `ITrainingSearchIndexer` | **integration** |

The audit handler is the interesting one, and it stays in-process deliberately. It writes an
`ILogger` line: nothing to roll back, no external state to reconcile, and a failure changes nothing
a user could observe. Routing it through the outbox would add a database row per rename in order to
produce a log entry. Should the audit trail ever become a queryable store of its own, it becomes an
integration and moves — that is a change of nature, and it will be visible as one.

### Design constraints the implementation must respect

- **The aggregate never raises an integration event.** It states a domain fact and stays ignorant of
  who listens. The translation belongs to the application layer, which is the only place that knows
  an email exists.
- **Integration events must not inherit `INotification`.** `IDomainEvent` does today, which couples
  the shared kernel to Mediator; it is tolerable for an in-process message and wrong for one that is
  serialised and read back by another process.
- **Delivery is at-least-once, so consumers must be idempotent.**
  `ITrainingSearchIndexer.IndexAsync` already is — it creates *or refreshes* an entry.
  `IEmailSender.SendAsync` is not, and will need a deduplication key rather than a promise.
- **One worker per host, one message per claim.** Both API hosts run the same code, so the worker
  will run twice; a message is claimed by a lease taken in the database, so a duplicate host does
  not mean a duplicate email. A separate deployable was the alternative and lost on the grounds that
  this solution has none and should not gain one to solve a problem a lease solves.

## Consequences

- A committed state change and the integration events it owes are one atomic fact. The welcome email
  for a trainer who does not exist becomes unrepresentable.
- Work survives a crash: the outbox row is the record that something is owed, and the worker resumes
  from it.
- The transaction gets shorter and stops depending on an SMTP server answering. Today a slow mail
  host holds a database transaction open.
- Failure of a side effect no longer aborts the business operation, which is the right way round —
  and also a behaviour change worth stating: today a failing indexer prevents a training from being
  created at all.

Against that:

- **The side effect becomes eventual.** Between the commit and the worker's pass, the email has not
  been sent. Nothing in this application needs it to be synchronous, but a reader must not assume it
  still is.
- **More moving parts**: a table, a migration, a worker, a lease, a retry policy, a poison-message
  outcome, and tests for each. The current design has none of that, which is exactly why it is
  wrong rather than simple.
- **At-least-once pushes correctness onto consumers.** Idempotency stops being a nicety.
- **Ordering is not guaranteed across aggregates.** Two events committed by different requests may
  be delivered in either order.
- **The outbox lives in `TrainingContext` and covers only it.** `TrainingIdentityDbContext` is a
  separate context: an operation spanning registration and trainer creation is still not atomic, and
  this decision does not change that.

## Status of the implementation

Not implemented. This record is the decision, not a description of the code.

Until the outbox exists, the four integration handlers **still run inside the transaction**, and the
two defects described above are still live. That gap is stated here rather than left for a reader to
discover, and closing it is separate work.

## Alternatives considered

**Keep everything in-process and document the limitation.** The cheapest option, and the one this
record replaces. It leaves a demonstrably wrong behaviour in a repository whose purpose is to show
how to get these things right.

**Dispatch after the commit instead of before.** Moving the interceptor to `SavedChanges` would stop
emails going out for transactions that fail. It costs almost nothing — and solves half the problem:
there is still no record of what is owed, so a process that dies after the commit loses the reaction
permanently. It would also break the domain reactions, which need to be *inside* the transaction to
be atomic. Half a fix that damages the working half.

**Publish to a message broker at commit time, without an outbox.** Moves the gap rather than closing
it: the commit and the publish are still two operations that can disagree, one of them now over the
network.

**Two-phase commit across the database and the mail or index provider.** Not available with these
providers, and not desirable if it were.

**Event sourcing.** Would make every event durable by construction and remove the need for an
outbox on the write side. It is a different persistence model altogether, and rewriting the
aggregates to get a delivery guarantee would be a large change to obtain a small one.

**Let the aggregate raise integration events directly.** Removes the translation step, at the price
of teaching the domain that emails and search engines exist. The domain does not need to know who
listens, and the moment it does, that knowledge spreads.
