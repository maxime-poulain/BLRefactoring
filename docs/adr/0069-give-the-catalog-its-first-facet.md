# 0069 — Give the catalog its first facet

- **Status:** Accepted — amended by [0080](0080-let-a-visitor-browse-several-shelves-at-once.md): the topic filter becomes a selection joined by *or*, and the facet counts stop being the whole catalog's to answer the term the visitor typed
- **Amends:** [0059](0059-give-the-search-index-a-body-and-a-query-surface.md)
- **Date:** 2026-08-10

## Context

The strategic design is explicit about what the announced Catalog Discovery context still lacks:
*"what is missing here is no longer a store: it is a page, its facets, its ordering by anything
other than a title."* A visitor who knows a title can find it — ADR 0059 built the index, ADR 0062
the screens — but a visitor who does not know what they are looking for has nothing to browse. The
domain has held the browse dimension all along: `Topic`, a closed set of six values every training
declares between one and three of, refused at every boundary when a name is not the domain's own
spelling. Nothing public served it.

The context's expected language names the word this record makes real: *facet*.

## Decision

**The search index gains its first non-title dimension, and the catalog serves it as facets.**

- **The index files each entry under its topics.** A third table, `TrainingSearchTopic`, keyed by
  the pair exactly as the tokens are (ADR 0059): a topic has no identity beyond the entry it
  belongs to and the name it is, and the pair makes an entry's topics a set. The indexer reads the
  names in the same projection as the title and rewrites them with every document — total, never
  incremental — and the rows ride the same cascading foreign key as the tokens.
- **The names stored are the domain's canonical spellings.** `Topic.TryFromName` compares ordinally,
  the boundary refuses anything it does not answer, so what reaches the index is `Topic`'s own form
  and a facet needs no translation on the way out.
- **The facets are one read of the index alone.** `ITrainingSearchQuery.FacetsAsync` counts offered
  entries per topic — the same composed visibility the search reads (ADR 0050, ADR 0056), so a
  suspension or a withholding moves the numbers the moment its consumer runs. A topic nothing
  offered declares is **absent rather than zero**: a facet is a way into the catalog, and an empty
  shelf leads nowhere. Both hosts publish it anonymously at `GET /Catalog/topics`.
- **The search takes a shelf.** `GET /Catalog/trainings` gains a `topic` filter — equality against
  the topics' own index, not a prefix, because a topic is a name from a closed set rather than a
  word somebody is still typing — composable with the term.
- **An unknown name is refused twice, and no list is restated.** `[KnownTopic]` answers at model
  binding by asking the domain, the shape `[KnownStatus]` set (ADR 0055) minus the reflection —
  one type owns every topic. The CQRS validator asks again at dispatch, for ADR 0046's reason: the
  application layer never assumes the boundary checked first.
- **`EveryTopicTheDomainDeclares_FitsTheIndexColumn` holds the seam nothing else would.** A new
  topic wider than the index column would fail at index time, in a consumer, on the first training
  filed under it — no build breaks and no suite goes red until then. The rule reads the bound off
  the configuration and measures every name the domain declares against it. The third table also
  joins the encapsulation rule's list, so no layer above the index may name it.

## Consequences

- **The first word of the Catalog Discovery language exists in running code.** A facet is not a
  `Topic`: the domain's value object says what a training may declare, the facet says what a
  visitor may browse. The context still owns no store and remains *announced* — what this narrows
  is its missing list, not its status.
- **No new fact, and no fact changed.** The indexer already reads the document back from the write
  model on every delivery (ADR 0059's own shape), so the same nine consumers that maintain the
  title maintain the topics. The events stay two identifiers.
- **The counts are the catalog's, not the current search's.** A term-scoped facet count — "of the
  trainings matching *design*, how many per topic" — is a different feature with a different
  response shape, priced when somebody wants it. Named so its absence reads as a decision.
- **One migration**, `AddTrainingSearchTopics`, and existing entries hold no topics until their
  training is next re-indexed. Acceptable for the same reason ADR 0059 accepted an empty index at
  birth: the index is rebuilt by ordinary facts, and this repository's deployments have no data to
  carry.

## Alternatives considered

**Facets inside the search response.** One request instead of two, and term-scoped counts for
free. It couples the facet list to the paged envelope both stacks share (ADR 0029), and every
search would pay for a group-by it rarely wants. The separate read keeps the envelope untouched
and the facets cacheable on their own terms.

**All six topics, zeros included.** A stable chip row. But a facet is a way into the catalog, and
a shelf that answers an empty page is a dead end the interface handed out; the port's contract —
absent rather than zero — makes the promise structural.

**A bitmask column on the entry.** One column, no join. It caps the set at the width of an
integer, makes the per-topic count a bit-counting scan no index serves, and stores a number where
the language stores a name.

**Topics carried on the integration events.** It would spare the indexer one projection and cost
the whole event vocabulary a payload migration — for data the indexer already reads in the same
query as the title. ADR 0059 chose the read-back precisely so facts could stay two identifiers.

## Verification

- **`EveryTopicTheDomainDeclares_FitsTheIndexColumn` watched failing** with the column bound
  temporarily narrowed under the longest name the domain declares, then restored.
- **SQLite facts** on the real schema: the indexer files an entry under each declared topic,
  rewrites them on re-index, cascades them on removal; the facet query counts only what is offered,
  answers alphabetically, and answers absence rather than zero.
- **End to end, on both hosts** (`CatalogSearchTest`, Docker-backed): a published training moves
  its facet count and a withdrawal takes the shelf away, through the outbox; a shelf search answers
  only trainings filed under it; an unknown topic is refused 400 at the door.
- **The screen**, in bUnit: one chip per facet with its count, a click narrowing to the shelf on
  page one, a second click lifting the filter.
