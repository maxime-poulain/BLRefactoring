# 0071 — Give the catalog a second published order

- **Status:** Accepted
- **Amends:** [0001](0001-paginate-on-the-query-side-over-a-total-order.md),
  [0029](0029-answer-a-list-the-same-way-on-both-hosts.md),
  [0059](0059-give-the-search-index-a-body-and-a-query-surface.md)
- **Date:** 2026-08-10

## Context

The strategic documents have named the same absence since the catalog got a face: *"an ordering by
anything other than a title."* ADR 0069 delivered the facets and ADR 0070 the person's page; the
order a visitor reads the shelf in is the last named word, and it has stayed singular for an
argued reason. `QueryableOrderingExtensions` declares one order for the aggregates and one for the
index, and its own documentation refuses a third *"without an argument"* — a total order being a
correctness property rather than a preference.

The argument for a second one is a visitor's question the title cannot answer: *what is new here?*
A returning visitor scanning an alphabetical list has no way to see what appeared since their last
visit short of remembering the whole shelf. Every catalog they have ever used answers it with a
"newest" order.

What "newest" means is the decision's whole substance. The index's rows have a writing date, and
it is the wrong one: the day a document was written into a read model is a fact about the read
model, and a catalog sorted by when the indexer happened to reach a row would reshuffle itself
after every replay — the exact reasoning `AlphabeticallyByTitle`'s remarks recorded when they said
the index holds no `CreatedOn`. The training, on the other hand, carries its own age: every
aggregate audits `CreatedOn`, and a training is born published (`Training.cs` states it in the
initializer), so the training's age *is*, for almost every training that ever existed, the moment
it went on offer. A training that was withdrawn and republished keeps its age under this reading,
and that is correct rather than a compromise: it is back, not new.

## Decision

**The catalog answers in one of two named orders — by title, the default, or newest first over the
training's own age — chosen from a closed set, never composed.**

- **The index stores the training's age.** `TrainingSearchEntry` gains `CreatedOnUtc`, read back
  from the write model beside the title, so a replay rewrites the same instant rather than a new
  one. The migration backfills the column from the `Training` table, because every entry describes
  a training the same database already holds.
- **The second order is a named extension, total like the first.**
  `QueryableOrderingExtensions.NewestOnOffer()` orders `CreatedOnUtc` descending, ties broken by
  identifier — the same tie-break every order here carries, for ADR 0001's reason: creation
  instants collide at the platform's timer interval, and a tied pair left to the server is a row
  on two pages or on none. A second index, `IX_TrainingSearchEntry_OfferedNewest`, mirrors the
  offered index's shape so a "newest" page is a seek and a walk too.
- **The port takes the choice as a value from a closed set.** `ITrainingSearchQuery.SearchAsync`
  gains a `CatalogOrder` — `Title` or `Newest` — and the port's line moves by one word: not a
  predicate, not a sort expression, but one of two named orders. A caller still composes nothing.
- **The wire vocabulary is the enum's own.** `?sort=` on `GET /Catalog/trainings`, refused at
  model binding by `[KnownSort]` when it names anything the set does not — `[KnownTopic]`'s
  sibling, with the enum standing where the value object stands there, so publishing an order is
  one member and no second list (ADR 0041). Case does not matter, unlike the topic's ordinal
  match, because a sort name is translated to a member at the boundary and stored nowhere. The
  CQRS validator asserts the closed set again (ADR 0046), since any integer casts into an enum.
- **Both hosts, one contract, one mapping** (ADR 0029). The layered service takes the question
  whole, as `CatalogSearchRequest` — its first application-owned parameter type, so the signature
  keeps saying which boundary it is on (ADR 0048) — and the CQRS query gains `Order`; both arrive
  at the same port with the same arguments.

## Consequences

- A visitor can ask what is new, and two visitors asking the same question read the same page —
  the order is total, shared by both hosts, and served by an index rather than a sort of
  everything the table holds.
- The closed set is the boundary's protection: `TheCatalog_OrdersOnlyByItsNamedOrders` refuses an
  order written inline over the entries, so the third order costs what the first two cost — a
  name, an argument, and a record.
- `QueryableOrderingExtensions`' remarks change from *"no third without an argument"* to *"no
  fourth"*, and this record is the argument the third one owed.
- The index carries one more column whose value it does not own. That is the read-back's ordinary
  shape — the title travels the same way — and the replay heals it the same way (ADR 0069).

## Verification

- `TheCatalog_OrdersOnlyByItsNamedOrders` (SearchIndexRules) — the reader names both published
  orders and composes none of its own; proved by mutation before it was trusted.
- `TrainingSearchQueryTests` — newest answers the youngest training first, ties break by
  identifier, and the order composes with the term, the topic and the page.
- `TrainingSearchIndexerTests` — the entry carries the training's own age, not the indexer's
  clock.
- `CatalogSearchTest` (TestKit, both hosts) — `?sort=newest` reverses a seeded catalog over HTTP
  through the outbox, and an unknown sort is refused at the door.
