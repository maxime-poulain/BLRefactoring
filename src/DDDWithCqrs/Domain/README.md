# BLRefactoring.DDDWithCqrs.Domain — intentionally empty

This project contains no code, and that is deliberate.

The domain is shared. Both stacks — the CQRS one under `src/DDDWithCqrs` and the layered one under
`src/DDD` — build on the same aggregates, value objects, domain events and repository ports, which
live in [`src/BLRefactoring.Shared.Domain`](../../BLRefactoring.Shared.Domain). That is the premise
of the whole repository: the two stacks are compared at equal business scope, so a rule written
twice would make every comparison meaningless.

## Why keep an empty project at all

Because separating reads from writes is a decision about the *application* layer, not about the
model. Keeping this slot empty is what makes that visible: the CQRS stack adds commands, queries,
handlers and a read side, and changes nothing about the aggregates. Deleting the project would blur
the boundary the repository exists to show.

It is a deliberate trade, not an oversight: one assembly compiled for nothing, in exchange for a
solution tree that matches the architecture it illustrates.

## What would belong here

Anything genuinely specific to the CQRS stack's write model. Nothing has qualified so far — and the
read side is not a candidate: `PagedQuery`, `PagedResult` and the query contracts live in
`DDDWithCqrs.Application`, and the handlers that translate them to SQL live in
`DDDWithCqrs.Infrastructure`. Neither is domain.

The assembly layout, and what this choice costs, are recorded in [`docs/adr/`](../../../docs/adr).
