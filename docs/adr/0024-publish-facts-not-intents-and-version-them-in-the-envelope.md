# 0024 — Publish facts, not intents, and version them in the envelope

- **Status:** Accepted — the email half of "the ports remain fakes" is dated by [0031](0031-send-email-over-smtp-and-prove-it-against-a-real-server.md); the search half stays true; the retry contract gains its schedule in [0033](0033-back-off-between-retries-log-the-poison-and-sweep-the-delivered-history.md); the per-consumer half of its at-least-once promise is made true by [0034](0034-deliver-once-per-consumer-not-once-per-message.md)
- **Date:** 2026-08-04

## Context

ADR 0002 decided the split — domain reactions stay in the transaction, integration events go
through a transactional outbox — and deliberately stopped there. It never said what an integration
event *is*: what vocabulary it speaks, what identity it travels under, how it is serialized, how a
message written today is read by the code of next year. Implementing the write side forced each of
those decisions, and none of them is small enough to leave to the diff. This record carries them;
ADR 0002's decision text is untouched and still governs the split itself.

Two of them were live design arguments rather than details. The first: the initial sketch named the
messages by their intended effect — `WelcomeEmailRequested` — and the argument that killed it is
recorded below. The second: whether integration events should reuse the in-process messaging
library, since the delivery worker will need to route messages to handlers eventually — also
below, also rejected, and the constraint it defends was already written in ADR 0002.

## Decision

**The outbox carries facts, not instructions.** `TrainerCreated`, `TrainerContactEmailChanged`,
`TrainingCreated`, `TrainingEdited` — the same business facts the domain events state, translated
for the outside. Not `WelcomeEmailRequested`: a message named after its consumer's job freezes that
job into the producer, and every new reaction becomes a producer change instead of a new
subscriber. The context map already says which way this must go — *the catalogue is upstream of
everything else: it publishes facts, and the consumers adapt* — and a fact is the only shape that
serves Notification and the announced Catalogue Discovery context with one vocabulary. What the
welcome email says, and whether one is sent at all, becomes the consumer's decision, made after the
commit.

**An integration event is a sealed, immutable record of a fact that already happened**, and the
payload is primitives only, so a consumer deserializes the fact without the domain model. This is
the deliberate mirror of the domain-event rule: a domain event carries value objects because its
handlers share the model; an integration event carries `Guid` and `string` because its consumers
must not have to. The translation happens in the domain event handler — the last place the value
objects are in scope and the first place they must not leak.

**The marker inherits nothing.** `IIntegrationEvent` is empty and touches no messaging library —
ADR 0002 already required that a serialized message must not carry `INotification`, and this
implementation keeps the requirement structural: since no integration event is an `INotification`,
`IMediator.Publish(integrationEvent)` does not compile, and the pre-commit bus cannot be handed a
post-commit message by accident. The two species of event stay two species the compiler can tell
apart.

**Identity belongs to the envelope, not the payload.** A message is stored as an `OutboxMessage`
row:

| Column | What it is |
|---|---|
| `Id` | Minted at publish time, a version-7 GUID — **the deduplication key** consumers use to make at-least-once delivery safe. Time-ordered, so the clustered key follows insertion order. |
| `Name`, `Version` | The stable wire identity, resolved through the registry — never a CLR type name, which a refactoring could silently change. |
| `Payload` | The event as JSON, written and read back by one serializer. |
| `OccurredOnUtc` | When the fact was recorded — `datetime2(7)`, the same full-precision decision as the audit columns (ADR 0005) — and the order the worker delivers in. |
| `ProcessedOnUtc` | `NULL` while delivery is owed; the worker stamps it when done. |
| `Attempts`, `Error` | The retry contract: each delivery try counts, the last failure is kept beside the message it poisoned. |

A filtered index over the unprocessed rows (`ProcessedOnUtc IS NULL`, keyed by `OccurredOnUtc`)
answers the only question the worker will ask — what is owed, oldest first — and stays small no
matter how much history the table accumulates.

**Every integration event travels under a stable name and version declared in one explicit
registry.** `IntegrationEventTypes` maps each CLR type to its wire identity and back, by hand. No
assembly scanning, no naming convention: an implicit registration is a decision nobody reviews, and
this repository refuses those elsewhere for the same reason. The registry's completeness is a build
failure, not a runtime one (`EveryIntegrationEvent_HasAStableName`), and its reverse direction —
`Resolve(name, version)` — is the worker's read path, proven by round-trip tests before the worker
exists.

**Versioning is additive until it cannot be.** A change that old readers survive — a new optional
member — keeps its version. A breaking change registers the new shape under a bumped version, and
the old entry stays until no stored message carries it: entries outlive the events they name.

**What stays owed, and under which contract.** The delivery worker remains ADR 0002's promise: one
per host, one message per claim, a lease taken in the database — the lease columns arrive with the
worker, in the migration that gives them a writer. Delivery is at-least-once; consumers deduplicate
by `Id`. `Attempts` and `Error` are declared now, written by nothing yet, so the schema is one
migration and the retry contract is written where the columns are. On the consuming side the worker
gets its own contract — an `IIntegrationEventHandler<TEvent>` in the application layer and an
explicit dispatcher built on the registry's `Resolve`, some thirty lines — rather than the
messaging library's, for the reason in the alternatives.

## Consequences

- A committed change and the facts it owes are one atomic row set, in the same `SaveChanges` — the
  property ADR 0002 wanted, now held by tests from the change tracker up to a lost optimistic-
  concurrency race.
- **Until the worker exists, nothing sends and nothing indexes.** The four handlers that used to
  act now record; the facts accumulate, unprocessed, and will be delivered when the worker lands.
  That is a behaviour change and it is the intended direction: a record of what is owed replaces a
  side effect that could fire for a transaction that never committed. `IEmailSender` and
  `ITrainingSearchIndexer` stay registered with their fakes, idle, as the ports the worker will
  call.
- The welcome email's wording left the codebase with the handler that composed it. It returns with
  the worker, on the consuming side of the fact — which is where the decision to send anything
  belonged all along.
- A fifth integration event costs: the record, a registry entry, a handler that publishes it, a
  round-trip test row, and a mention on the event-storming board — each one enforced by a rule or
  a guard, none discoverable only in review.
- Two vocabularies now exist deliberately: domain events speak value objects inward, integration
  events speak primitives outward. The pair of rules that enforce the mirror
  (`EveryDomainEvent_CarriesOnlyDomainTypes`, `EveryIntegrationEvent_CarriesOnlyPrimitives`) is the
  boundary, executable.

## Alternatives considered

**Name the messages after their effect — `WelcomeEmailRequested`.** The outbox becomes a deferred
task queue: the producer decides every reaction at translation time, the worker merely replays port
calls. Simpler for the worker, and rejected for what it does to the boundary: the producer must
know each consumer's job, a new reaction means a producer deployment, and a read-side context like
Catalogue Discovery can subscribe to nothing — there is no fact left to subscribe to. ADR 0002
rejected letting aggregates raise integration events because *the moment it does, that knowledge
spreads*; naming messages by intent spreads the same knowledge one layer higher.

**Let `IIntegrationEvent` inherit `INotification` and reuse the Mediator pipeline for delivery.**
The worker would get `Publish` for free. Rejected because the free routing costs the boundary: the
moment an integration event is an `INotification`, publishing one in-process — inside the
transaction, as if it were a domain event — compiles everywhere, and the wall between the
pre-commit bus and the post-commit outbox becomes a convention instead of a type error. This
repository's habit is to make the wrong thing inexpressible rather than inadvisable. What the
worker actually needs — per-message try/catch, attempt counting, error recording, marking
processed — Mediator does not provide, so the hand-written dispatcher was owed anyway; the routing
it adds on top is the small part.

**Use CLR type names as wire names.** Free, automatic, and wrong the day a type is renamed: every
stored message now names a type that does not exist. A wire name must survive refactoring, which
is what the registry is.

**Forward every domain event automatically.** A generic handler translating all six events by
convention removes the four explicit translators — and with them the two decisions each translator
makes: whether this fact leaves the context at all (two deliberately do not), and what its outside
shape is. Selection and translation are decisions; a convention hides both.

**Source-generate the JSON contracts.** `JsonSerializerContext` would make serialization
reflection-free and AOT-ready. Rejected as ceremony this solution has no use for: a partial class,
generated members and per-type attributes, to optimize a code path that writes one small row per
business fact. The serializer is one static class with both directions and a round-trip test per
event; that is the amount of machinery the problem deserves.
