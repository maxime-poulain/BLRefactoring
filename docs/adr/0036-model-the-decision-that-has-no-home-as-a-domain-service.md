# 0036 — Model the decision that has no home as a domain service

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

A trainer hands one of their trainings over to a colleague. The business allows it under two
conditions, both about the *recipient*: their catalogue must still have room (fewer than
`Training.MaximumPerTrainer`, the rule ADR 0030 carved into creation), and it must not already
list the training's title (the per-trainer uniqueness every write already honours).

That decision has no home. `Training` cannot own it: the facts it needs — how many trainings the
recipient publishes, whether the recipient lists this title — are about a catalogue the aggregate
does not belong to, and a factory signature cannot carry them because nothing is being created.
`Trainer` cannot own it either: it holds no trainings, deliberately, and ADR 0030 already
rejected coupling the two aggregates to make it otherwise. The application layer deciding was
rejected twice (ADR 0016, ADR 0030). Every previous rule found an aggregate to live in; this one
is the first that genuinely spans two.

ADR 0030 saw this case coming and left the door ajar. Its own alternatives section reads: "Evans
reserves domain services for operations that naturally belong to no aggregate; this operation
*is* `Training` creation, which has a home and a factory already enforcing a rule of the same
kind." Creation had a home, so the service was refused. Transfer has none — which is precisely
the operation Evans reserved the pattern for. This record walks through the door 0030 left ajar
and narrows that record; it does not reverse it. Every rule that has a home still comes to its
aggregate through a port.

## Decision

**A domain service exists only for a decision no aggregate can own: recorded, named a
DomainService, static, stateless, its ports arriving as parameters.**

- **`TrainingTransferDomainService` is the first, and the shape is the law.** The name carries
  the `DomainService` suffix in full — never a bare `Service` — so a reader can tell a domain
  service in the DDD sense from an application, infrastructure or any other kind of service at
  the name alone, before opening the file. A static class beside the
  aggregate it mutates, one public method:
  `TransferAsync(Training, TrainerId recipient, ITrainingCounter, IUniquenessTitleChecker, ct)`
  answering a bare `Result`. Static and stateless like the factory it mirrors — the signature is
  the complete inventory of what the decision asks of the world, the ports answer raw facts and
  the service decides, and the domain names no lifetime. It refuses a transfer to the current
  owner, a recipient at the limit (`Training.RecipientCatalogueFull` — its own code, because "delete
  one of yours" and "pick another colleague" are different instructions to a caller), and a
  recipient already listing the title (`Training.DuplicateTitle` — the same business sentence
  creation and edit refuse with, backed by the same unique index).
- **The aggregate's mutator is internal, so the service is the only public path.** ADR 0030's
  first objection was that a service makes the rule forgettable. Here the type system answers it:
  `Training.TransferTo` is `internal`, raises the fact and reassigns the owner, and nothing
  outside the domain assembly can reach it — reassignment without answering the recipient's
  questions does not compile. The event must be raised by the aggregate anyway
  (`AddDomainEvent` is protected), which is the design agreeing with itself.
- **0030's other two objections, answered in kind.** A second home for creation rules? These are
  not creation rules — they are recipient-side facts no factory signature could carry. The
  anemic slide? The rule below makes each new domain service a recorded decision: the ledger
  grows by argument, never by habit.
- **The rule becomes a ledger.** `TheDomain_NamesNoService` keeps defending ADR 0030 — the domain
  still declares no service to decide in an aggregate's place — but now pins the recorded
  exceptions by name and record, exactly as the aggregate-question rule pins `IsOwnedBy`. A new
  rule holds the recorded services to this record's shape: listed with their ADR, named a
  `DomainService`, static, stateless, every public method answering a `Result`. A service that
  appears without a record, or drifts from the shape — a bare `Service` name included — fails
  the build.

The recipient's *existence* is deliberately not the service's question: the decision — capacity
and uniqueness — is well-defined for any `TrainerId`, and referential integrity is the same
precondition creation already treats as orchestration. The application layer asks
`ITrainerRepository.ExistsAsync` and refuses with `Training.UnknownRecipient` before the service
is consulted, on both stacks.

## Consequences

- The transfer ships end to end: `POST /Training/{trainingId}/transfer` on both hosts, guarded by
  the `TrainingOwner` policy like every other write; no `If-Match`, mirroring delete — a transfer
  is an action on the resource, not an edit of its content, and the recipient-side checks are
  the contention that matters. It answers 204: nothing is created, and the giver can no longer
  read what they gave away.
- `TrainingTransferredDomainEvent` carries both owners; its integration twin re-indexes the
  training under the new one — the fake indexer's upsert converges, so the search side needed no
  change. The board gains its first multi-actor edge.
- The capacity pre-check can lose a race, exactly as ADR 0030 accepts at creation: two
  concurrent transfers to the same nearly-full recipient can land one training over the limit —
  visible, harmless, transferable onward. The title race is closed for real by the existing
  `(TrainerId, Title)` unique index: a lost pre-check surfaces at save as the same
  `DuplicateTitle` the pre-check would have answered.
- The Blazor front does not gain a transfer page: there is no trainer directory to pick a
  recipient from, and building one is a decision of its own.

## Alternatives considered

**A port that answers the whole question (`ITrainerCatalogueChecker.CanReceiveAsync`).** The
aggregate keeps the call, but the implementation owns the comparison and half the rule — the
exact division ADR 0030 forbids: ports answer raw facts, the domain decides.

**Hang the rule on `Trainer`.** The recipient aggregate deciding about a training it does not
hold, loaded and locked for every transfer — the two-aggregates-one-transaction coupling 0030
already refused when it kept the count off `Trainer`.

**Let the application layer decide.** Rejected twice already; a third refusal changes nothing.

**A static helper not named `Service`.** The same code dodging the rule by rename — worse than
either honest option, because the ledger exists precisely so the pattern is visible and counted.

**An instance service registered in the container.** A lifetime and two registrations for a
stateless decision; the static shape keeps the domain free of any container vocabulary.

## Verification

`TrainingTransferDomainServiceTests` prove the decision in memory: reassignment and the event with
both owners on success; refusals for a full recipient (the boundary passes at nine), a taken
title, and a self-transfer that consults no port; and that every refusal mutates nothing. The
TestKit proves the slice against SQL Server on both hosts: a training changes hands over HTTP,
the giver loses sight of it, the recipient's list gains it, and the transferred fact rides the
outbox. The rules were proven the honest way round: the coverage rule was red while this record
had no defender, `TheDomain_NamesNoService` went red the moment `TrainingTransferDomainService`
existed unpinned — the same bite 0030 recorded — and the new rule fails a de-pinned or
non-static service on demand.
