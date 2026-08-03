# TrainingHub.DDD.Infrastructure — intentionally empty

This project contains no code, and that is deliberate.

Persistence is shared. The `DbContext`, the entity configurations, the migrations, the repository
implementations, the unit of work and the interceptors that dispatch domain events and stamp audit
columns all live in
[`src/TrainingHub.Shared.Infrastructure`](../../TrainingHub.Shared.Infrastructure), because
both stacks store the same aggregates in the same schema. Two sets of mappings would be two things
to keep in step, and the first divergence would be a silent one.

## Why keep an empty project at all

Because it names the layer and marks where a layered stack expects to find it, keeping the solution
tree faithful to the architecture it illustrates. It also holds the seam: should the layered stack
ever need persistence of its own, it has a home that does not disturb the shared one.

Note the contrast with the CQRS side. `DDDWithCqrs.Infrastructure` is *not* empty — it holds the
query handlers, the Mediator pipeline behaviors and the paging extensions, because a read side is
infrastructure by nature: it projects rows onto DTOs and carries no business rule. The layered
stack has no read side, so it has nothing to put here. That asymmetry is the point rather than an
accident.

## What would belong here

Anything specific to how the layered stack persists or reads, and nothing else. Shared mappings and
shared repositories stay shared.

The assembly layout, and what this choice costs, are recorded in [`docs/adr/`](../../../docs/adr).
