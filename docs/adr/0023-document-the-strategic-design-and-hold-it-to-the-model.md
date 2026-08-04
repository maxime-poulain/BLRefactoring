# 0023 — Document the strategic design, and hold it to the model

- **Status:** Accepted
- **Date:** 2026-08-03

## Context

This repository demonstrates the tactical half of Domain-Driven Design thoroughly and the strategic
half not at all. Aggregates, value objects, typed identifiers, domain events, specifications and
repositories are all visible in the code and described in the README. Where the boundaries are, what
each one owns, in whose language, and which are bought rather than built — none of that is written
anywhere, and none of it can be inferred from a class.

The gap has a cost that is easy to underestimate. A reader who wants to know what this system is
*about* has to reconstruct it from twenty-six projects. A reader who wants to know whether the
design is any good has to reverse-engineer the boundaries before they can judge them. And the
boundaries here are not obvious: the most interesting one is invisible, because it separates the
domain from a framework rather than one project from another.

There is a second, sharper reason. The strategic decisions in this codebase are already made and
already argued — in code comments. `Trainer.ContactEmail` explains that a contact address is not a
credential and that authentication belongs to the Identity context. `ICurrentUserService` explains
that conflating an account with a trainer is what lets one caller edit another's work.
`ITrainingSearchIndexer` explains why it speaks primitives. Those are strategic statements scattered
across three files, each visible only to whoever happens to open it.

## Decision

### The documentation lives in `docs/strategic-design/`, not among the records

Four documents: an entry point, the bounded contexts, the context map, and an event storming of the
two main flows.

They sit beside `docs/adr/` rather than inside it because they are a different kind of writing. A
record captures a decision at a moment: what was open, what each option cost, why the loser lost —
and it is never rewritten, because the reasoning that was true then is what makes a later change
legible. A strategic-design document is the opposite: it describes the model *as it is now*, and it
is expected to be edited whenever the model moves. Numbering it as a record would promise
immutability it must not have.

`docs/` rather than the README for the same reason the records are not in the README: the README is
read front to back by someone evaluating the repository, and adding fifteen minutes of domain
analysis to it would bury the thing they came for. The README gains one pointer.

### Nothing is documented that the code does not honour

The single largest risk in a document like this is inventing boundaries. A system with two
aggregates can be written up as four bounded contexts by anybody willing to type, and the result
reads well and describes nothing.

So the analysis is constrained to what the model can be shown to say:

- **`Trainer` and `Training` are one context, not two.** They are mapped in one `DbContext`, commit
  in one transaction, reference each other by typed identifier, and one cascades into the other
  *inside the same unit of work*. Three properties, each checkable, and each incompatible with a
  boundary.
- **Identity & Access is a genuine second context.** Its own `DbContext`, its own migration history,
  and a domain that knows it only through `UserId` and `ICurrentUserService`. The anti-corruption
  layer is two types wide, and the code already explains why it exists.
- **Notification, Search Indexing and Media Storage are generic subdomains**, each behind a port,
  two of them with nothing but a fake implementation. They are on the map because the ports are real
  decisions; they are marked as unimplemented because pretending otherwise would be the same lie in
  the other direction.

### What is announced is separated from what is speculation

*Catalogue Discovery* — the public site — is treated at the same level as the built contexts,
because three existing decisions were made for it: an index maintained on every write and read by
nobody, a photo endpoint addressed by identifier with an immutable cache, and a query side that
projects without loading aggregates. A reader who does not know they were made for a public
catalogue reads them as over-engineering.

*Scheduling* and *Enrolment* are named in a section called **Not decided** and kept off the map. The
model's silence points at them — a `Training` has no date, no capacity, no price, and there is no
participant anywhere — but a silence is not a plan. Putting them on the map would make a hypothesis
look like a roadmap.

### An event storming is included, and its limit is stated

On two aggregates and six events there is nothing left to discover, so the boards make no claim to
be a workshop record. They are included because this codebase has an unusual property: its reactions
are already named as policies — `SendWelcomeEmailWhenTrainerCreated`,
`NotifyPreviousAddressWhenTrainerContactEmailChanged`. The *when this, then that* notation maps
one-to-one onto files that exist, which turns the boards into a reading aid rather than an exercise.

Two boards, not a wall: becoming a trainer, and publishing a training. Open questions are marked as
hotspots rather than smoothed over — the welcome email sent inside the transaction, the deletion
that never reaches the index, the rule for removing a trainer that no actor can trigger.

### The documents answer to a test

[ADR 0013](0013-make-every-record-answer-to-a-test.md) makes every record in force answer to a rule,
and `TheDiagram_DescribesExactlyTheEdgesTheProjectsDeclare` compares the README's graph edge by edge
with the real project references. Documentation that nothing checks is documentation that drifts,
and this repository's whole argument is that it does not accept that.

`StrategicDesignRules` therefore holds three claims:

| Rule | What it catches |
|---|---|
| `EveryAggregate_IsPlacedInExactlyOneBoundedContext` | A third aggregate that belongs, on paper, to no context — or to two |
| `EveryDomainEvent_AppearsInTheEventStorming` | A new business fact the boards never mention |
| `EveryContextOnTheMap_HasItsOwnSection` | The map and the descriptions drifting apart |

The first is the one that matters. Aggregates are the unit a boundary is drawn around, so an
aggregate nobody placed is the exact moment this document stops being true.

## Alternatives rejected

**A section in the README.** Rejected on length. The README is already the longest document here and
is read by someone deciding whether to keep reading; fifteen minutes of domain analysis in the
middle of it serves neither audience.

**Records numbered 0023 to 0028, one per context.** Rejected on kind. A context description is not a
decision with alternatives — it has no loser to record — and the records' own convention forbids
rewriting them, which is precisely what a description of a moving model needs.

**Documentation with no rule behind it.** Rejected as the failure this repository was built to
demonstrate against. It would have been quicker by an hour and wrong by the third aggregate.

**Modelling the boundaries in code — one project per context, one schema per context.** Rejected as
out of proportion, and deliberately so. The boundaries described here are real and are worth
knowing; enforcing them structurally would cost a project split, a second database and a saga at
registration, in exchange for nothing this system needs at its current size. The document says where
the lines are; it does not pretend they are walls.

## Consequences

**The strategic decisions already argued in code comments now have one address.** The comments stay
where they are — they are load-bearing where a reader meets them — and the documents cite them
rather than paraphrasing.

**A new aggregate now costs a documentation edit, and the build says so.** That is the intended
price. The alternative is a document that is correct on the day it is merged and quietly wrong
afterwards.

**Three of the six contexts on the map have no implementation.** Notification and Search Indexing
run on fakes, and Catalogue Discovery does not exist. The map marks all three, because a map that
does not distinguish what is built from what is intended is worse than no map — it is a map that
lies without ever being wrong about anything in particular.

**The event-storming boards will need editing before they need rewriting.** They cover two flows out
of two; a third flow — anything to do with a public catalogue, or a participant — arrives as a third
board rather than as a change to these.
