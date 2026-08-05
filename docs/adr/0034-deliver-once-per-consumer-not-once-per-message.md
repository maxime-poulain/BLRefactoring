# 0034 — Deliver once per consumer, not once per message

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

ADR 0024 promised that "delivery is at-least-once; consumers deduplicate by `Id`", and ADR 0025
built the worker that delivers. Reading the two against the code they produced exposes three
related defects, all hiding behind the same lucky accident: today every integration event has
exactly one production consumer, so none of them has fired yet.

**One throwing consumer aborts its neighbours.** The dispatcher hands a fact to each registered
consumer in order, with no isolation of any kind: the first one that throws ends the loop, and
every consumer after it never runs that pass.

**A retry replays consumers that already succeeded.** The failure is recorded on the envelope —
the only ledger there is — so the next attempt re-runs *every* consumer of the message. The day
the welcome email and a second reaction share `TrainerCreated`, a failing neighbour re-sends the
welcome on every retry until the budget runs out.

**The promised deduplication key is unreachable.** `IIntegrationEventHandler<TEvent>.HandleAsync`
receives the fact and a token — never the envelope's id. The payloads cannot smuggle it either
(they carry business primitives only, by rule). So "consumers deduplicate by `Id`" was a contract
no handler could sign: zero of the four deduplicate, and two say so in their own remarks. What
held the system together was convergence — the index upsert tolerates replay — and the modest
stakes of a duplicate email.

One more small debt rides along: the claim orders by `OccurredOnUtc` alone. Two rows written in
the same tick have no defined order, and the envelope's id — a version-7 GUID minted at publish
time precisely so that key order follows insertion order — was never asked to break the tie.

## Decision

**Delivery is settled per consumer, in a ledger the platform owns — the promise the handlers
could never keep is kept for them.**

- **A delivery ledger beside the envelope.** `OutboxMessageConsumer` records one row per
  (message, consumer) delivery: `MessageId`, `ConsumerName`, `DeliveredOnUtc`. Its key is the
  pair itself — the repository's first composite key, deliberately: a per-consumer delivery has
  no identity beyond the two things it joins, and a surrogate would be a column with no reader.
  The composite key also answers the processor's only question (all consumers of one message) as
  a leftmost-prefix seek, so the table needs no other index. A foreign key with cascade delete
  ties the ledger to its envelope in the database engine itself: the retention sweep of ADR 0033
  and any operator's `DELETE` clean the ledger for free, through plain SQL, with no change
  tracker involved.
- **The dispatcher isolates and reports; the processor records.** Dispatch takes the set of
  consumers already delivered and answers an outcome: who delivered this pass, who failed and
  with what. A consumer already in the ledger is skipped; a consumer that throws no longer stops
  its neighbours — the loop continues and collects. Cancellation is the one exception that still
  propagates whole: it is a shutdown, not an outcome, and re-running a pass it aborted is exactly
  the at-least-once window the lease already implies. The processor writes a ledger row per
  delivered consumer, marks the message processed when every consumer has settled, and otherwise
  records the failure on the envelope as before — the attempt counted once per message, the
  reasons joined per consumer. Poison keeps its ADR 0033 meaning — budget spent, still owed — and
  its ledger rows now tell the operator exactly *which* consumers are owed. Deserialization and
  routing failures still fail the whole message: they happen before any consumer runs, and there
  is no outcome to split.
- **A consumer's ledger identity is a hand-written stable name, never a type name a refactoring
  could change.** `IIntegrationEventHandler<TEvent>` gains `ConsumerName`, and each handler
  declares its own — `"SendWelcomeEmail"`, `"IndexTraining"` — a string literal beside the
  reaction it names, in the mould of the error codes each aggregate owns (ADR 0015). ADR 0024's
  argument against CLR wire names applies verbatim to ledger identity ("wrong the day a type is
  renamed") — and would not even fit: the longest current handler `FullName` is 130 characters,
  wider than the 128 every other name column in the outbox settled on.
- **The claim's order gains its tiebreaker.** `ORDER BY OccurredOnUtc, Id` — the id is minted in
  insertion order for exactly this kind of duty. SQL Server and .NET compare GUIDs differently,
  but each side is deterministic, which is all a stable order requires; nobody should chase
  cross-engine agreement for a tie between two rows born in the same hundred nanoseconds.

The rule `EveryIntegrationEventConsumer_OwnsAStableName` defends the identity half: every
consumer implementation — test doubles included — declares its name as a literal, unique among
the consumers of its event, and short enough for the column that stores it. The isolation half is
behaviour, held by the dispatcher's unit tests and the TestKit fact in Verification.

## Consequences

- A retry crosses no delivered consumer twice: the welcome email of a message whose neighbour is
  failing goes out exactly once, and the Mailpit assertion in the TestKit can finally say so —
  before this record a duplicate would have passed every suite silently.
- "Consumers deduplicate by `Id`" stops being folklore about handlers and becomes a mechanism of
  the platform: handlers stay free of bookkeeping, take no `TrainingContext`, and the rule that
  forbids them committing is untouched. Handler idempotency drops from obligation to
  defence-in-depth.
- The envelope's story is unchanged for everything that reads it — attempts, backoff, poison,
  sweep all still work per message — but a lapsed lease's benign double-delivery window narrows
  from "the message in flight" to "the consumers of the message in flight that had not yet
  settled". The race where two claimants insert the same ledger row resolves in the database: the
  composite key refuses the second, the save fails into the drain's catch, the next poll finds
  the ledger already honest.
- One more table rides the outbox's lifecycle. It needs no sweep of its own — the cascade ties
  its rows to the envelope's — and its size is bounded by the same retention that bounds the
  history.

## Alternatives considered

**Pass the envelope id into `HandleAsync` and let each handler deduplicate.** This is what the
old promise implied, and it is the wrong division of labour: N handlers each maintaining a copy
of the same ledger, the application contract widened for bookkeeping, and the abort defect —
the first thrower killing its neighbours — untouched.

**Continue-on-error without a ledger.** An `AggregateException` fixes the abort and nothing
else: the retry still replays every consumer, which is the defect with consequences.

**Fan out one outbox row per (message, consumer) at publish time.** Per-consumer isolation by
multiplication: N rows for one fact, the envelope's identity split, the fact/subscriber symmetry
ADR 0024 bought gone. The ledger keeps one fact one row and moves only the delivery state down a
level.

**A messaging library.** The same answer 0024 and 0025 gave, unchanged: what this system needs
is thirty lines and two tables it fully understands.

**CLR type names as ledger identity.** Rejected by 0024's own argument, and by arithmetic — 130
characters does not fit a 128 column.

**A central consumer-name registry, mirroring `IntegrationEventTypes`.** The registry exists for
wire names because resolution needs a reverse direction and one table two artifacts restate. A
consumer name is only ever compared for equality, and a static application-side table could
never name the TestKit's failing consumer — the very double the isolation proof requires. The
name belongs to its owner, like an error code.

## Verification

The dispatcher's unit tests hold the isolation in memory: a throwing consumer does not stop its
neighbour, an already-delivered consumer is not run again, and the outcome names both the
delivered and the failed. `EveryIntegrationEventConsumer_OwnsAStableName` holds the identity —
red on all four handlers before the member existed, red again when a name is flipped to
`nameof`. The TestKit proves the whole against SQL Server on both hosts: a registration whose
test-only neighbour fails its first delivery ends processed with one attempt spent, a ledger
naming both consumers, and exactly one welcome email in the mailbox; the retention fact proves
the cascade by planting a ledger row on the swept envelope and finding none after.
