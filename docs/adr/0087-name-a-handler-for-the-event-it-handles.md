# 0087 — Name a handler for the event it handles

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

This repository has two families of event handlers, and they spell the same idea differently.

The nineteen integration event consumers embed the full type name of the fact they consume:
`SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler` handles
`TrainerCreatedIntegrationEvent`, and the event's name can be read back out of the handler's,
letter for letter, between `When` and `Handler`.

The eighteen domain event handlers truncate it. `DeleteTrainingWhenTrainerDeletedEventHandler`
handles `TrainerDeletedDomainEvent` — the name says `EventHandler` where the event says
`DomainEvent`, so the one word that classifies the type is the word the name drops. Read in
isolation, `AuditWhenTrainerSuspendedEventHandler` does not say which kind of event it reacts to,
and in a codebase where the two kinds run at opposite moments — a domain event handler inside the
transaction, an integration event consumer after the commit, with entirely different rules about
what each may do (ADR 0002) — the classification is not pedantry. It is the first thing a reader
needs.

The truncation also breaks the symmetry the event-storming documentation leans on: the boards
translate *when X then Y* policies one-to-one onto files, and the translation is cleanest when the
`X` in the file name is exactly the event type.

What the names must keep is the reaction. Four domain events have two handlers each — suspension,
reinstatement and withholding each pair an audit line with a publisher, and the trainer's deletion
pairs the cascade with one — so a handler named after its event alone, `TrainerSuspendedDomainEventHandler`,
cannot exist twice. Only the policy phrase tells `AuditWhenTrainerSuspended…` from
`PublishIntegrationEventWhenTrainerSuspended…`, and collapsing the pairs into one handler apiece
would be a structural change wearing a rename's clothes.

## Decision

**A handler is named `{Reaction}When{Event}Handler`, where `{Event}` is the handled event's full
type name.** For a domain event that ends the name in `DomainEventHandler`; for an integration
event, in `IntegrationEventHandler` — the suffix is not a rule of its own but what falls out of
embedding the event's name whole:

- `TrainerDeletedDomainEvent` → `DeleteTrainingWhenTrainerDeletedDomainEventHandler`
  and `PublishIntegrationEventWhenTrainerDeletedDomainEventHandler`
- `TrainerNameChangedDomainEvent` → `AuditWhenTrainerNameChangedDomainEventHandler`
- `AccountErasedIntegrationEvent` → `SendErasureNoticeWhenAccountErasedIntegrationEventHandler`,
  which is what it was already called — the integration side conforms today and is pinned rather
  than renamed.

So a type name answers, on its own and in order: what it does, when it does it, and which kind of
event that is — which is the moment it runs and the rules it runs under.

## Consequences

- **Eighteen classes and their files are renamed** in `Shared.Application/EventHandlers/` — the
  five audit lines, the twelve publishers and the cascade — by inserting `Domain` before
  `EventHandler`. Their tests are renamed with them.
- **Nothing else moves.** The handlers are discovered by interface — `IDomainEventHandler<TEvent>`
  through the Mediator source generator — so no registration, dispatch or configuration names a
  handler type; the count stays eighteen, so no rule-audited counter moves; the event-storming
  boards write policies in the short form (`DeleteTrainingWhenTrainerDeleted`), which the renamed
  files still begin with.
- **The nineteen integration consumers change by not being allowed to change**: the same rule that
  forces `Domain` into the eighteen holds `IntegrationEventHandler` onto the nineteen, so the
  symmetry this record restores cannot be lost from either side.
- **Records merged before this one keep the names they were written with.** ADR 0056 names
  `PublishIntegrationEventWhenTrainerSuspendedEventHandler` as it was called on the day it was
  accepted; a merged record is never rewritten, so the defending rule reads code and never
  `docs/adr/`. The README's handler table and the event-storming design table are living documents
  and move with the code.

## Alternatives considered

**`{Event}DomainEventHandler`, bare.** The shape the suffix convention suggests first, and
impossible here without merging the four paired handlers — a change to how many reactions exist,
what each is registered as, and what its tests cover. A naming convention that requires
restructuring is not a naming convention. Rejected.

**Bare names where an event has one handler, policy names where it has two.** Reads fine per file
and incoherently per folder: two naming shapes for one kind of thing, and adding a second handler
to an event would force a rename of the first — a neighbor's edit changing a file that did not
change. Rejected.

**Enforce only the suffix.** A rule demanding `DomainEventHandler` at the end would be satisfied by
`SomeDomainEventHandler` — the vague name the convention exists to prevent, wearing the right
suffix. Embedding the event's full type name is what makes the name informative rather than merely
classified, and the suffix comes free with it. Rejected as the weaker rule that costs the same.

## Verification

`EveryHandler_IsNamedForTheEventItHandles` censuses every implementation of
`IDomainEventHandler<TEvent>` and `IIntegrationEventHandler<TEvent>` across the backend assemblies
and asserts each type's name ends with `When{typeof(TEvent).Name}Handler` — one assertion carrying
the reaction-first shape, the event linkage and both suffixes at once.

Born red against exactly the eighteen domain event handlers, with all nineteen integration
consumers already green, and green once the eighteen were renamed. The rename is
behavior-preserving — discovery is by interface — so the proof that nothing but names moved is
that every suite passes unchanged apart from its own renames.
