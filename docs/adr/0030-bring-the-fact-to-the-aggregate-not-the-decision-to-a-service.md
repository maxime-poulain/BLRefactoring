# 0030 — Bring the fact to the aggregate, not the decision to a service

- **Status:** Accepted
- **Date:** 2026-08-04

## Context

A new business rule arrived: **a trainer cannot publish more than ten trainings.** It is the
second rule of its kind in this domain. Like title uniqueness before it, the decision belongs to
one aggregate's creation, but the fact it decides on — how many trainings this trainer already
publishes — lives in rows the aggregate cannot see.

The textbook answer for a rule that "spans aggregates" is a Domain Service, and the question was
put exactly that way: should a `TrainingCreationService` sit beside the aggregates and hold the
rule? This record answers the question once, because the first rule of this kind (title
uniqueness, in place since the beginning) already answered it implicitly and nothing wrote the
reasoning down — a reader meeting a capacity check inside a factory, with a Domain Service
nowhere in sight, could reasonably take the absence for an accident.

Looked at closely, the rule does not span two aggregates. `Trainer` is neither loaded nor
changed by it; nothing about a trainer decides whether an eleventh training may exist. The rule
constrains **the set of trainings of one trainer** — precisely the set that title uniqueness
already constrains, and that rule lives in the `Training` aggregate and owns its error code
(`Training.DuplicateTitle`, ADR 0015). "A trainer publishes at most ten trainings" and "a
trainer's titles are unique" are two sentences about the same set, and they should live in the
same place.

## Decision

**The rule lives in `Training.CreateAsync`, and the fact comes to it through a port.**

- `ITrainingCounter`, declared in the domain beside the aggregate, answers one question: how
  many trainings a trainer currently publishes. `TrainingRepository` implements it, exactly as
  it implements `IUniquenessTitleChecker`.
- The port answers the **raw count**, not "is the catalogue full": an implementation that
  answered the decision would own half of it. The comparison against the limit happens in the
  factory, in the domain, in one place.
- The limit is a named business concept, not a magic number: `Training.MaximumPerTrainer`,
  a constant on the aggregate that owns the rule. The refusal is
  `Training.CatalogueFull` (ADR 0015: the aggregate's own prefix), and its message carries the
  limit so a caller learns the rule rather than just the refusal.
- The check runs at creation only. Editing changes a training, never how many there are; the
  count is asked before the content is looked at, because no title makes an eleventh training
  acceptable. A catalogue that held more than ten trainings before the rule existed keeps them —
  the constant is a bound on growth, not a licence to delete.

**No Domain Service.** Three reasons, in the order that decided it:

1. **A service makes the rule forgettable.** `Training.CreateAsync` is the only way a
   `Training` comes to exist, and its signature now demands a counter: creation without
   answering the capacity question does not compile. A `TrainingCreationService` would guard
   only the callers that remember it exists — and the second stack, or the third use case, is
   exactly where somebody forgets.
2. **Two homes for creation rules is one too many.** Title uniqueness already lives in the
   factory. Moving capacity to a service would split rules of the same species across two
   places, and every future rule would reopen the question this record closes.
3. **It is the first step of a familiar slide.** A service that decides beside the aggregate
   grows a second decision, then a third, and the aggregate ends as data behind an anemic
   façade. The evented cascade, the specifications and the factories all pull the other way in
   this codebase; a Domain Service here would be the one artefact pulling back.

The rule `TheDomain_NamesNoService` defends the decision: no type declared in the domain ends in
`Service`.

## Consequences

- Creation-time rules keep one shape a reader can learn once: value objects validate the parts,
  the factory enforces what crosses rows, and each cross-row fact arrives through a port named
  after its question. The factory's parameter list is the complete inventory of what creation
  asks of the world.
- The application layers stay orchestration: both stacks changed by one injected dependency
  passed through, and neither contains the rule.
- **The pre-check can lose a race.** Two requests arriving at nine trainings can both count
  nine and both create, ending at eleven. Title uniqueness has a unique index as its
  authoritative backstop; a count has no such constraint to lean on, and serialising creations
  per trainer would buy a lock nobody has asked for. The limit is a catalogue policy, not a
  security bound — the accepted worst case is one training over, visible and deletable, and the
  door to revisit first is a filtered check inside the same transaction if the rule ever
  hardens.
- The port is one more interface implemented by the same repository class. That is the pattern's
  price everywhere it is used; the alternative — passing `ITrainingRepository` itself into the
  factory — would hand the aggregate a whole query surface to enforce one rule.

## Alternatives considered

**A Domain Service (`TrainingCreationService`).** The intuition the record exists to answer —
rejected for the three reasons above. Evans reserves domain services for operations that
naturally belong to no aggregate; this operation *is* `Training` creation, which has a home and
a factory already enforcing a rule of the same kind.

**Holding the count on the `Trainer` aggregate.** The one option with real transactional teeth:
a counter column on the trainer row, incremented under its concurrency token, makes the race
unwritable. Rejected because it buys that safety by coupling two aggregates in one transaction —
`Trainer` would change on every training creation — and this codebase's aggregates react to each
other through events (ADR 0002), never inside one commit. The cost would also be permanent
schema and a hot row per trainer, for a rule whose breach is one visible training over a soft
limit.

**A specification carrying the rule.** `TrainingCatalogueFullSpecification` cannot exist under
ADR 0028's own terms: a specification is one expression answering a predicate over a candidate,
in memory and as criteria. "Count the rows and compare to ten" is not a predicate over any
single `Training` — forcing it into the pattern would reopen the query-DSL door that record
closed.

**Enforcing in the application layer.** Both stacks check the count before calling the factory.
Rejected without much debate: the rule would exist twice, could drift twice, and the domain —
whose one job is to be the place where business rules are true — would not contain the rule the
README then claims it holds.

**A database constraint as the authoritative guard.** What the unique index is for uniqueness, a
trigger or filtered check would be for capacity. Rejected for now: it moves a business rule into
SQL where no test names it, and the race it closes has a bounded, reversible cost. Recorded here
as the first thing to reach for if that stops being true.

## Verification

`TrainingTests` proves the rule in memory against a mocked counter: nine published answers
success, ten answers `Training.CatalogueFull` with the message naming the limit, and a full
catalogue refuses before looking at the content. `CatalogueCapacityTest` in the shared TestKit
proves the whole chain on both hosts against SQL Server: ten trainings are created one POST at a
time — each answer asserted, so a guard that tripped early would fail the walk — and the
eleventh leaves as a problem document carrying the domain code. `TheDomain_NamesNoService` holds
the design at build time, and was broken once (a `Service`-named type declared in the domain) to
prove it bites.
