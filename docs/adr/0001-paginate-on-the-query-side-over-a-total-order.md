# 0001 — Paginate on the query side, over a total order

- **Status:** Accepted — amended in part by [0029](0029-answer-a-list-the-same-way-on-both-hosts.md)
- **Date:** 2026-08-01

## Context

Four list endpoints answered with the whole table: every trainer, every training, every training of
a trainer, every training on a topic. That is a defect that grows with the data and reports nothing
until it does.

The repository hosts two application stacks over one shared domain — a layered one built on
application services, and a CQRS one built on commands and queries. Whatever bounds those reads has
to be placed in one of them, or in the domain both share, and that placement is the decision.

Paging is also not only about how many rows come back. `OFFSET`/`FETCH` over a query whose `ORDER BY`
does not distinguish every row lets the server return ties in whatever order suits it, so the same
row can appear on two pages while another appears on none. It passes every test, never reproduces
locally, and surfaces as an item missing from a list.

## Decision

**Paging lives on the query side of the CQRS stack, and nowhere else.**

- `PagedQuery` carries the page a query asks for; every list query derives from it.
- `PagedResult<T>` carries the page answered, with `TotalPages`, `HasNextPage` and `HasPreviousPage`
  derived rather than stored — two numbers that must agree are two numbers that can disagree.
- `PagedQueryableExtensions.ToPagedResultAsync` counts, then fetches, then projects.
- `QueryableOrderingExtensions.NewestFirst` defines the order: `CreatedOn` descending, ties broken
  by identifier.

**The repositories, the specifications and the domain model are untouched.** No rule in this domain
operates on aggregates by the page, so teaching the repositories to return pages would grow the
domain's surface to serve a screen.

**Only the CQRS host pages.** The layered host still answers its lists with a plain array. Giving it
a paged read side would make it CQRS and dissolve the comparison this repository exists to make.

**A total order is a type-level requirement, not a convention.** `ToPagedResultAsync` takes an
`IOrderedQueryable<T>`, not an `IQueryable<T>`, and `NewestFirst` is the only thing that produces
one. A handler cannot page without going through it, so the ordering mistake described above is
unwritable rather than merely discouraged.

**The tie-break by identifier is required.** `CreatedOn` is not unique and cannot be made so:
`TimeProvider.GetUtcNow` reads the system clock, which advances at the platform's timer interval
(around 15.6 ms on Windows by default), so every write landing in one tick shares an instant;
concurrent requests collide just as easily; there is no index on the column, and a unique one would
fail concurrent inserts. Stamping each entity from its own clock reading — see
`AuditableEntitiesInterceptor` — does not change this, and the tie-break survived that change.

**The maximum page size holds twice.** `PaginationRequestHttp` declares it as a data annotation, so
an out-of-range value is rejected at model binding with the parameter named and the limit reaches
the OpenAPI document. `PagedQuery` bounds it again on construction, so a query built in code is
paged rather than unbounded by omission; a size of zero or less — what an unset field looks like in
code — falls back to the default rather than to a one-item page.

## Consequences

- A new list endpoint on the CQRS side is a query deriving `PagedQuery` and a handler calling
  `NewestFirst` then `ToPagedResultAsync`. A new aggregate costs a call site, not a new definition.
- Both hosts serve the same REST API except on the shape of a list. That asymmetry is now stated in
  the README rather than left for a reader to discover.
- Two round trips per page, deliberately: a `COUNT`, then the page. EF Core drops the ordering when
  translating the count, so no `ORDER BY` is paid for twice.
- The projection is an expression applied after `Skip`/`Take`, so SQL selects only the columns the
  DTO needs, for the rows of that page alone. No aggregate is materialised.
- Deep pages will degrade. `OFFSET n` makes the server walk and discard `n` rows, which is the
  accepted cost at this size and the thing to revisit first if the data grows.

## Alternatives considered

**Paging in the repositories or the specifications.** Rejected: it would put a presentation concern
into the write model that both stacks share, for a need no use case has. Being able to add paging to
one side without touching the aggregates, the repositories or the specifications is precisely what
separating reads from writes buys, and spending that on the first list endpoint would have been
spending it for nothing.

**Paging both hosts, for parity.** Rejected: the layered host reads through repositories, whose job
is to hand back aggregates. The asymmetry is the clearest thing this repository has to say about
what the two approaches cost and buy.

**Keyset (cursor) pagination.** Not chosen now: it does not degrade on deep pages, but it cannot
answer "page 7 of 12", and it needs exactly the stable total order established here. That order is
therefore the prerequisite this decision leaves in place, and switching later is a change to
`ToPagedResultAsync` and the request contract, not to the handlers or the domain.

**One statement instead of two, with `COUNT(*) OVER ()`.** Rejected: it puts a window function on
every row of the page. Two small, index-friendly statements are the better trade at this size and
easier to read in a profiler.

**Ordering on `CreatedOn` alone.** Rejected, and re-examined when per-entity clock stamping was
introduced on the assumption it would make timestamps unique. It does not — see above. The
identifier costs nothing: same composite `ORDER BY`, same index, no extra query.

**Two non-generic overloads of the ordering method, one per aggregate.** Rejected in favour of one
generic method constrained to `AggregateRoot<TEntityId>`. The cost is that both type arguments must
be written at each call site — `Id` is declared on `Entity<TEntityId>` and its type *is* that
parameter, and C# infers type arguments from the arguments alone, never through constraints, so
`TEntityId` has no inference source. The alternative that does infer, `EF.Property<Guid>(x, "Id")`,
buys inference by turning a compiler-checked member access into a string that still compiles after a
rename and fails at runtime.

**Naming the ordering method `OrderByCreationDateThenById`.** Rejected: it restates the
implementation in the signature, so changing the order would mean renaming every call site or
leaving a name that lies; it reads as one ordering among several when the point is that there is
exactly one; and it is inaccurate, since the order is descending. `NewestFirst` names what the
caller gets, and the tie-break — a correctness detail, not a caller's choice — is documented on the
method.

## Verification

Six unit test methods cover the arithmetic and the bounds. The claim that actually matters is
covered by an integration test against SQL Server:
`PaginationTests.WalkingEveryPage_ReturnsEachItemExactlyOnce` creates five trainings in a tight
loop — so several share a `CreatedOn` — walks three pages, and asserts no identifier is seen twice
or lost. It only means something against a real server: an in-memory provider preserves insertion
order, so a query missing its `ORDER BY` looks correct there.
