# 0029 — Answer a list the same way on both hosts

- **Status:** Accepted — amended by [0071](0071-give-the-catalog-a-second-published-order.md): the shared list contract gains a sort parameter — the same closed set of orders on both hosts
- **Date:** 2026-08-04

## Context

ADR 0001 put paging on the query side of the CQRS stack and declared the asymmetry deliberate:
"Only the CQRS host pages. The layered host still answers its lists with a plain array." That
sentence was defensible when it was written and it stopped being true to the repository's own
claims as the repository grew around it.

Two promises fell out of agreement with it:

- **The README promises the two stacks are compared on identical ground** — same domain, same
  REST API, so that the difference a reader studies is the application style and nothing else.
  On `GET /Training/my-trainings` the ground was not identical: one host answered a page envelope
  with metadata, the other the whole table. A client written against one host broke against the
  other on the very operation the comparison is supposed to illuminate.
- **ADR 0006 and ADR 0008 promise that a client generated from either host fits both**, and the
  generated client is built from the layered host alone. Its `GetMineAsync` therefore returned
  `List<TrainingResponseHttp>` and could not deserialize the CQRS envelope — the parity claim was
  falsifiable with one call, and `BothHosts_PublishTheSameOperations` never noticed because it
  compares operation identifiers, not shapes. `OwnTrainingsTest` in the shared TestKit had to
  document the fork in its own remarks: the same suite asserted two different response bodies for
  the same operation depending on the host under test.

Beyond the broken promises, the layered read was the last unbounded list in the API: a defect
ADR 0001 opened by naming it — "a defect that grows with the data and reports nothing until it
does" — and then left in place on one of the two hosts.

## Decision

**Both hosts answer the same list the same way: one page of a total order, in one envelope.**
This record amends ADR 0001. What survives of it is everything that made paging correct — the
total order (`NewestFirst`: `CreatedOn` descending, ties broken by identifier), the type-level
guard (`ToPagedResultAsync` takes an `IOrderedQueryable`), the bounds held twice (data annotation
at the boundary, clamping in code), the derived metadata, the two round trips. What falls is
"Only the CQRS host pages" and "the repositories are untouched".

**Paging becomes kernel vocabulary.** `PageRequest` and `PagedResult<T>` move to
`TrainingHub.Shared/Common/Pagination`, beside `Result`: the page a caller asks for and the page
they get are the same idea in both stacks, and defining them twice would be the duplication the
kernel exists to prevent. `PagedQuery` dissolves — the CQRS list query now *holds* a
`PageRequest` (initialised, so a query built bare still asks for the default page) rather than
deriving from a base class, which is composition where inheritance bought nothing. The ordering
and paging extensions move to `Shared.Infrastructure`, where both hosts reach them.

**The repository gains a named question, not a query surface.** `GetPageByTrainerIdAsync`
answers "the page of this trainer's trainings, newest first" — the order is fixed inside the
implementation, only the page coordinates travel, and no criteria, sorting or projection can be
passed in. That is a named question in exactly the sense ADR 0028 requires, and none of what
ADR 0001 refused: no includes, no ordering parameters, no query DSL grew on the contract. The
unpaged `GetByTrainerIdAsync` stays, because the deletion cascade genuinely needs every training
and a page-walking loop there would be ceremony.

**The HTTP contracts move where shared contracts live.** `PaginationRequestHttp` and
`PagedResponseHttp<T>` leave the CQRS host for `Shared.Api/Contracts/Pagination`, which is where
they always claimed to belong — a contract published by both hosts, held in the project both
reference.

**The comparison the repository exists to make survives — sharpened.** ADR 0001 feared that
paging the layered host "would make it CQRS and dissolve the comparison". It does not: the
layered service still reads through the repository and still hands back what a repository hands
back — aggregates, here a page of them, mapped to DTOs in the service. The CQRS handler still
projects columns straight to DTOs before the page is fetched. Same contract on the wire,
different cost underneath: the layered page materialises whole aggregates (owned `Topics`
included) to serve a list, the CQRS page selects the columns the DTO names. That is a more
honest statement of what the two styles cost than an asymmetry a client trips over.

Two rules defend the decision: `BothHosts_AnswerEachOperationWithTheSameShape` (every operation
published by both hosts declares the same responses on each) and `NoAction_AnswersABareCollection`
(a success answer is never a bare array or list — an unbounded read has no contract to hide in).

## Consequences

- The generated client fits both hosts again, on every operation. `GetMineAsync` takes the page
  coordinates and returns the envelope, whichever host serves it — ADR 0006 and ADR 0008's parity
  claim is back to being true, and now guarded by shape, not just by operation identifier.
- The Blazor page consumes the envelope and walks pages; without that, a trainer with more
  trainings than one page would silently lose access to the tail.
- The last unbounded list in the API is bounded. The cap (`PageRequest.MaxPageSize`) holds on
  both hosts through the one shared type.
- The layered stack pays the aggregate bill for its page — whole `Training` aggregates with their
  owned collections, for a screen that shows four columns. That is not an oversight; it is the
  layered style's price made visible on identical ground, and the reason the CQRS handler's
  projection exists.
- `PaginationTest` moved to the shared TestKit: the walking-every-page proof, the metadata proof
  and the cap rejection now run against both hosts instead of one. `OwnTrainingsTest` no longer
  documents two shapes.
- The kernel grew two types. They are vocabulary, not behaviour — the same standing `Result` has —
  and the alternative was importing them from one stack into the other, which is a dependency
  direction this repository refuses.

## Alternatives considered

**Keep the asymmetry and re-state it louder.** Tenable only while nothing promised parity. The
generated client is built from one host and used against both; `OwnTrainingsTest` already had to
fork its assertions per host. The asymmetry was not a documented difference anymore — it was a
defect with a paragraph explaining it.

**A read-side port for the layered stack** — an `ITrainingReader` beside the repository,
returning DTO pages. That is a query handler wearing a service name: the layered stack's defining
trait is that use cases go through application services and repositories, and giving it a
separate read path would make it CQRS with extra steps — dissolving the comparison in exactly the
way ADR 0001 warned amending it would.

**Paging in memory in the application service** — call the unpaged repository question, then
`Skip`/`Take` in the service. Dishonest: the database still reads and ships every row, so the
defect ADR 0001 names is intact and the envelope's metadata claims a bound that does not exist.
Rejected without hesitation.

**A specification that carries paging** (Ardalis-style, `Skip`/`Take`/ordering on the
specification). Re-opens the door ADR 0028 just closed: a specification names a business rule,
and "page 3, twenty at a time, newest first" is not a rule of this domain — it is a screen's
coordinates. Paging stays a parameter of a named question, never a property of a specification.

## Verification

`PaginationTest` in the shared TestKit runs the walking-every-page proof (five trainings created
in a tight loop, several sharing a `CreatedOn`, three pages walked, every identifier seen exactly
once), the metadata proof, the default-page proof and the cap rejection against **both** hosts,
against a real SQL Server. `PageRequestTests` and `PagedResultTests` cover the bounds and the
arithmetic once, in the kernel's suite. The two architecture rules above hold the shape parity
and the no-bare-collection claim at build time, on every operation rather than the one this
record fixes.
