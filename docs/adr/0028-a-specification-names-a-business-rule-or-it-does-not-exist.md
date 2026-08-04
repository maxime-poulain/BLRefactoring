# 0028 — A specification names a business rule, or it does not exist

- **Status:** Accepted
- **Date:** 2026-08-04

## Context

This repository has carried a Specification machinery for a long time: `ISpecification<T>` in the
kernel reduced to an `Expression` criteria, a `SpecificationEvaluator` translating it to
`IQueryable`, generic `GetAsync(ISpecification)`/`AnyAsync(ISpecification)` members on the shared
`IRepository<T>`, and two concrete specifications. One of them
(`TrainingTitleExistsForTrainerSpecification`) was the data half of a real invariant; the other
(`TrainingsByTrainerSpecification`) stated no rule at all — "the trainings of X" is scoping, not a
decision — and the generic repository members let any caller compose arbitrary criteria. That is
the pattern's known failure mode: a query DSL wearing a domain word, one convenient criteria
object at a time, until nobody can say what a specification *means*.

The ask was to make the pattern mean something: specifications as business rules in the layered
stack, never as query builders, with the CQRS readers left reading directly. The domain itself is
deliberately small — one non-trivial invariant (title uniqueness, already enforced by the
`Training` aggregate) and one sentence said everywhere without ever being named: *a training
answers only to the trainer that published it*, written as a claim comparison in the HTTP
ownership policy, as an inline identifier comparison in the layered use case, and as SQL scoping
in the CQRS readers.

## Decision

**A specification names a business rule, or it does not exist.** The rule-less filter is deleted;
its `Where` clause went back inside the repository implementation, where queries live. No
specification is invented to demonstrate the pattern either: "a trainer may create a training" is
not a rule this model has, and writing `TrainerCanCreateTrainingSpecification` would have been
ceremony pretending to be domain knowledge.

**One concept, two evaluations.** A specification carries a single statement of its rule — the
expression handed to the base constructor — and answers it two ways: `IsSatisfiedBy` compiles it
(lazily, once) for in-memory decisions; `Criteria` exposes it for the repository implementation
that must ask the database before loading anything. There is no second hierarchy of "query
specifications": one species, defined by naming a rule.

**Specifications live in the domain, beside their aggregate.** `TrainingOwnedBySpecification`
finally names the ownership sentence; `TrainingTitleExistsForTrainerSpecification` remains the
data half of the uniqueness invariant, whose decision stays in the aggregate. Rule in the
aggregate, criteria in the specification: neither restates the other.

**An aggregate may wear a specification as a question.** `Training.IsOwnedBy(trainerId)` delegates
to the ownership specification, so a use case asks the object it holds instead of instantiating
machinery. This is a recorded exception to "an aggregate never hands data back": the rule that
enforces that line pins the question by name, so the next one arrives with a record of its own —
the shape "any bool method" is exactly how reading state through methods would creep back in.

**The DSL's doors are closed.** `IRepository<T>` is gone, and with it the generic
specification-taking members; each repository interface declares the named questions its use
cases actually ask, and consumes specifications inside its implementation. The
`SpecificationEvaluator` went with it — handing `spec.Criteria` to a `Where` needs no evaluator.
The CQRS readers keep their direct EF queries, untouched.

Three rules defend the decision: `EverySpecification_IsDeclaredInTheDomain`,
`NoQueryHandler_TouchesASpecification`, `NoRepositoryContract_TakesASpecification`.

## Consequences

- The ownership sentence exists once, named, and the layered use case reads as the decision it
  is: `training.IsOwnedBy(caller)` — with the HTTP policy and the CQRS readers keeping their own
  forms deliberately, because authorization and read-scoping are not the domain speaking.
- Specifications are tested in memory, without EF: build the aggregate through its own factories,
  ask `IsSatisfiedBy`. The expression side needs no separate proof of *translation* — the
  integration suites drive the repositories that consume `Criteria` against a real SQL Server.
- The write side of the CQRS stack shares the rules automatically, because they live in the
  shared domain. What stays layered-only is consumption style; what stays CQRS-only is reading
  without any of it.
- Two fewer kernel/infrastructure types (`IRepository<T>`, `SpecificationEvaluator`) and one
  fewer specification than before, for a pattern that now says more. Growth is deliberate:
  a new specification must name a rule, live beside its aggregate, and — if an aggregate is to
  wear it — bring its record.

## Alternatives considered

**A specification library (Ardalis.Specification).** Brings back everything ADR 0001 and this
record remove — includes, ordering, paging, projection — as first-class members of every
specification, plus a repository base whose surface is the generic query methods just deleted.
The library is good at being a query object model; a query object model is what this decision
refuses.

**Two hierarchies: business specifications and query specifications.** An honest-looking
compromise that institutionalises the drift instead of stopping it — every future filter becomes
a "query specification", and the interesting half of the pattern dissolves. One species, and
filters stay `Where` clauses.

**Composability (`And`/`Or`/`Not`) in the base class.** The classic completion of the pattern,
left out on purpose: two rules exist today and no use case combines them. Combinators are an
invitation to build query trees — the DSL again — and adding them the day two rules genuinely
compose is a small, recorded change.

**Inventing creation rules to showcase the pattern.** `TrainerCanCreateTrainingSpecification`
with no rule behind it. The event storming records refusals as deliberately as reactions; a
specification without a rule would be the model lying about itself.

**Keeping the aggregate out of it (spec-only consumption).** Callers evaluate
`TrainingOwnedBySpecification` directly and `Training` says nothing about its own ownership. One
artifact fewer, but the aggregate stops being the place where its rules are visible — the use
case reads better asking the object, and the pinned-question rule keeps the surface from growing
unrecorded.
