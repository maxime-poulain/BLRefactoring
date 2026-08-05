# 0025 — Deliver the outbox with a hosted service in each host

- **Status:** Accepted — the email half of "they remain fakes" is dated by [0031](0031-send-email-over-smtp-and-prove-it-against-a-real-server.md); the search half stays true
- **Date:** 2026-08-04

## Context

ADR 0002 promised the outbox's read side — "one worker per host, one message per claim, a lease
taken in the database" — and ADR 0024 wrote the contract it would honour: `Attempts` and `Error` as
the retry ledger, the envelope's id as the consumer's deduplication key, an
`IIntegrationEventHandler<TEvent>` for the consuming side. What remained open was the machinery:
what runs the loop, and whether a scheduling library should run it instead. The question was asked
concretely — a Hangfire recurring job was on the table — so this record answers it concretely.

## Decision

**A `BackgroundService` in each API host.** `OutboxDeliveryWorker` is registered by
`AddInfrastructure`, so both hosts run one without either doing anything: the two stacks stay
symmetric by construction. Two workers over one table is not a hazard to design away but the first
proof of the delivery semantics: competing consumers are safe because the claim is.

**The claim is one statement, and the lease is in the database.** A batch is claimed with a single
`UPDATE … OUTPUT` over the oldest unprocessed rows, under `UPDLOCK, READPAST, ROWLOCK`:
competing workers skip each other's locked rows instead of queueing on them, and the same statement
that reads the rows writes `ClaimedBy` and `ClaimedUntil`. A worker that dies mid-batch takes
nothing with it — its lease lapses and the rows return to the pool. This is the one query in the
solution that EF Core cannot express, and it stays raw SQL where the persistence already lives.

**Every registered event is consumed** — the four consumers are the very policies the write side
detached (welcome the trainer, warn the old address, index, reindex), reattached on the consuming
side of the fact, where at-least-once applies: the email pair tolerates a rare duplicate, the index
pair upserts to convergence. A fact nobody consumes is a decision that was never finished, and a
rule says so (`EveryIntegrationEvent_IsConsumed`). **A consumer never commits** — it runs after the
transaction it reacts to, so a consumer that writes through the unit of work would open a
transaction nobody scoped; a rule holds that line too (`NoIntegrationEventHandler_Commits`).

**Routing is a switch, not a scan.** `IntegrationEventDispatcher` lists the registered events and
hands each to its `IIntegrationEventHandler<TEvent>` registrations. The set of integration events
is closed and explicit in the registry; the dispatcher restating it where the routing happens is
the same decision written at its second seam, and a unit test holds the two lists together.

**Outcomes are saved per message, and failure is an outcome.** Success stamps `ProcessedOnUtc`;
whatever a consumer threw is recorded on the envelope — the attempt counted, the reason kept, the
lease released so any worker may retry. A message whose attempts exhaust `MaxAttempts` is poison:
still stored, no longer claimed, its last error sitting beside it for the operator. Saving after
each message rather than each batch keeps the redelivery window to the single message in flight.

**Cadence and budgets are configuration.** `OutboxOptions` (poll interval, batch size, attempt
budget, lease duration) binds from the `Outbox` section; the defaults are seconds because a
mechanism whose point is surviving restarts should not pretend to be a message bus. The clock is
`TimeProvider`, the timer a `PeriodicTimer` driven by it, and the integration suites shrink the
interval to milliseconds without touching production defaults.

## Consequences

- ADR 0002 is now implemented end to end: the facts committed by the write side are delivered,
  at-least-once, after the commit — the welcome email answers a trainer that exists, the index
  only ever learns of trainings the database accepted.
- `IEmailSender` and `ITrainingSearchIndexer` have callers again. They remain fakes that write to
  the log; choosing a provider remains a one-line registration.
- Delivery is eventual, by seconds under the defaults. Anything needing read-your-writes belongs
  on the query side, not in a consumer.
- A poison message halts nothing and alerts nobody — it waits in the table for an operator. A
  dead-letter surface (an endpoint, a metric, an alert) is deliberately not designed here; the day
  it is wanted, the rows it would read already exist.
- The worker polls. Two hosts polling every five seconds is a trivial load against a filtered
  index that holds only owed rows; if the table ever earns push semantics, that is a new decision.

## Alternatives considered

**A Hangfire recurring job.** The suggestion that prompted this record. Hangfire would supply the
recurrence — and only the recurrence, which is the trivial tenth of the problem. The claim, the
lease, the idempotence and the poison policy would still have to be written, but now against a
second persistence model: Hangfire brings its own tables, its own serialization of job arguments,
and above all its own retry machinery (`AutomaticRetry`, scheduled re-enqueues) that would compete
with the retry contract ADR 0024 already carved into the envelope — two ledgers for one question,
each ignorant of the other. Add a NuGet dependency with its own upgrade cadence, a dashboard that
becomes an operational surface, and storage schemas this repository would carry forever, all to
replace a `PeriodicTimer`. A scheduler earns its place in a system that has *jobs* — reports,
clean-ups, billing runs. This system has one queue, and it is already a table.

**Quartz.NET.** The same trade with a different dashboard: cron expressions and clustered
schedulers are machinery for a scheduling problem, and draining a table every few seconds is not
one.

**Dispatch in-process right after the commit** (a post-`SaveChanges` hook handing the fresh rows
to the dispatcher). Lowest latency, no polling — and it quietly reintroduces the failure mode the
outbox exists to close: the process that just committed is the only one trying to deliver, and if
it dies, delivery waits for nobody-knows-what. The table is the source of truth precisely so that
*any* worker, later, delivers what *this* process committed. A latency-driven hybrid (hook plus
polling safety net) doubles the paths for no recorded need.

**A dedicated worker host.** A third executable that owns delivery exclusively. Cleaner isolation,
and the right shape the day delivery needs its own scaling or deployment cadence — but today it
means a new project, a new deployment, and the loss of the competing-consumers demonstration the
two existing hosts provide for free. The worker is one class behind `AddInfrastructure`; promoting
it to its own host later is a move, not a rewrite.

**SQL Server change tracking / Service Broker for push delivery.** Wake the worker on insert
instead of polling. Both bind the solution to deep SQL Server machinery for a latency win nothing
here has asked for; the filtered index keeps the poll cheap enough to be boring.
