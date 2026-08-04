# TrainingHub.DDDWithCqrs.Infrastructure

The read side of the CQRS stack, plus the Mediator plumbing it runs on.

Shared persistence — the `DbContext`, the entity configurations, the migrations, the repositories,
the unit of work and the save-time interceptors — lives in
[`src/TrainingHub.Shared.Infrastructure`](../../TrainingHub.Shared.Infrastructure) and is used
by both stacks. What is here is what only the CQRS stack has.

## What it holds

- **`Features/**/…QueryHandler`** — one handler per query. They read through the `DbContext`
  directly and project onto DTOs; no repository is involved, because a repository's job is to hand
  back aggregates and a screen does not need one.
  The paged reads ride `NewestFirst` and `ToPagedResultAsync` from
  `TrainingHub.Shared.Infrastructure/Pagination` — shared with the layered repository since
  [ADR 0029](../../../docs/adr/0029-answer-a-list-the-same-way-on-both-hosts.md); the order and
  the envelope are [ADR 0001](../../../docs/adr/0001-paginate-on-the-query-side-over-a-total-order.md)'s.
- **`ThirdParty/Mediator/`** — the `ICommandDispatcher`/`IQueryDispatcher` implementations, and the
  pipeline behaviors for validation and for disabling change tracking while a query runs.

## Why the query handlers are here rather than in Application

A command handler orchestrates business: it loads aggregates, calls their behaviour and commits
through the unit of work, and it needs no knowledge of EF Core. It belongs in Application, and that
is where it is — in the same file as its command.

A query handler has no business to orchestrate. It *is* data access: an `IQueryable`, a projection,
a translation to SQL. Placing it in Application would mean either referencing EF Core from
Application, or inventing a read abstraction whose only purpose is to hide an `IQueryable` behind
something less capable. Neither buys anything.

The asymmetry is therefore intentional: the query side is thin enough that its handler and its
infrastructure are the same thing.
