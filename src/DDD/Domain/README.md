# TrainingHub.DDD.Domain — intentionally empty

This project contains no code, and that is deliberate.

The domain is shared. Both stacks — the layered one under `src/DDD` and the CQRS one under
`src/DDDWithCqrs` — build on the same aggregates, value objects, domain events and repository
ports, which live in [`src/TrainingHub.Shared.Domain`](../../TrainingHub.Shared.Domain).
That is the premise of the whole repository: the two stacks are compared at equal business scope,
so a rule written twice would make every comparison meaningless.

## Why keep an empty project at all

Because the shape of a layered architecture is part of what this repository demonstrates. Deleting
it would leave `src/DDD` with an Api and an Application layer and no visible domain, which reads as
if the layered stack had none — the opposite of what it shows. The empty project marks the slot,
names the layer, and points here.

It is a deliberate trade, not an oversight: one assembly compiled for nothing, in exchange for a
solution tree that matches the architecture it illustrates.

## What would belong here

Anything genuinely specific to the layered stack's model — a rule that only makes sense without a
read side. Nothing has qualified so far. If something ever does, it belongs here rather than in the
shared domain, precisely so the shared one stays the common denominator.

The assembly layout, and what this choice costs, are recorded in [`docs/adr/`](../../../docs/adr).
