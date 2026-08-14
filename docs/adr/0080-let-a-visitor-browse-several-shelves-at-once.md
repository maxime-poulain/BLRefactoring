# 0080 — Let a visitor browse several shelves at once

- **Status:** Accepted
- **Amends:** [0069](0069-give-the-catalog-its-first-facet.md),
  [0073](0073-describe-the-catalog-to-the-machines-that-read-it.md)
- **Date:** 2026-08-14

## Context

ADR 0069 gave the catalog its first facet and a filter to go with it: one `topic`, one shelf. The
row of chips that came out of it looks like a set of checkboxes and behaves like a set of radio
buttons — ticking *ASP.NET Core* while *C#* is lit does not add a shelf, it replaces one. A visitor
who wants what sits on either has no way to ask for it, and the only reading of the row that makes
its behavior predictable is the one nobody would guess from looking at it.

The counts have the matching problem, and ADR 0069 named it while declining to fix it: *"the counts
are the catalog's, not the current search's."* A visitor who types `event` sees every chip still
advertising the whole catalog, so a shelf reading *Design (12)* can answer a page of nothing. That
record priced the term-scoped count as "a different feature with a different response shape". The
first half turned out to be true and the second false — the shape does not change at all, only what
the count is taken over.

## Decision

**The topic filter becomes a selection, it widens rather than narrows, and the facets are counted
under the search the visitor has typed.**

- **Several shelves, joined by *or*.** A training answers as soon as it sits on **at least one** of
  the ticked topics. That is the opposite of how the term's words compose — every word must match —
  and the two are deliberately opposite: a word a visitor adds says more about what they want, a
  shelf they tick says they would also take what is on it. An intersection would make the second
  chip almost always answer nothing, a training declaring a handful of topics out of the sixteen
  the domain spells.
- **One `EXISTS` with an `IN` inside it.** The same shape a token already uses against the topics'
  own index, with equality rather than a prefix (ADR 0069's reason, unchanged: a topic is a name
  from a closed set, not a word somebody is still typing). Composed with the term rather than
  beside it, so the count and the page describe one set (ADR 0055).
- **The parameter stays singular and repeats.** `?topic=Design&topic=Programming`, never a joined
  string. A lone `?topic=Design` binds to a sequence of one, so every address already shared,
  bookmarked or indexed answers exactly what it answered before — no redirect, and no second
  spelling of the same question.
- **`[KnownTopic]` judges a sequence name by name.** It had a `string` arm and a catch-all that
  passed anything else; a bound collection would have fallen through the catch-all and passed
  unconditionally, which is the worst kind of failure this attribute can have — the boundary stops
  refusing unknown topics, silently, because passing is exactly what it is supposed to do when
  handed a type it has no opinion about. The CQRS validator asks again per element, for ADR 0046's
  reason.
- **The facets are counted under the term, and under the term alone.** They do not see the ticked
  shelves. Counting each topic against the selection would give every lit facet the size of the
  whole selection under a widening filter, and the figures would stop telling the shelves apart —
  the numbers must say *what this shelf would add*, which is what makes them worth reading while
  choosing. The endpoint therefore takes one parameter, the term, read exactly as the search reads
  it: both go through one private method rather than two copies of the same predicate, so a facet
  can never be counted under a wider question than the search answers. A shelf a term empties is
  **absent rather than zero**, which is ADR 0069's promise applying to a narrower population.
- **The canonical carries the whole selection, sorted.** ADR 0073 keeps `topic` and drops the term,
  the sort and the page; a selection changes nothing about that policy and everything about its
  spelling. The shelves are sorted ordinally wherever the address is written or read, so two
  visitors who ticked the same two chips in opposite orders share one address — otherwise one
  question would have a canonical per click order, which is the duplicate-URL problem the canonical
  exists to close.
- **The row gains one gesture back.** Clearing a selection chip by chip was free while only one
  could be lit; with several it is one click per shelf. A *Clear topics* chip appears while there is
  something to clear and not before, an escape from a state nobody is in being a filter of its own.

## Consequences

- **A published signature changes on both stacks**, from `string? topic` to a collection: the port,
  the two application layers, both controllers and the generated client. Nothing that spoke the old
  wire format has to change, which is the point of keeping the parameter singular.
- **One decision reverses**, and it is worth naming as a reversal rather than an addition: ADR 0069
  recorded catalog-wide counts as the deliberate choice. They were the deliberate choice for a
  filter that could hold one shelf; under a selection, a chip is something a visitor reads *before*
  deciding, and a number that ignores what they have typed is a number that misleads them.
- **`GetCatalogTopicsQuery` stops being an empty marker and gains a validator.** ADR 0046's line is
  that a validator guards what a message carries, so a message that starts carrying something starts
  needing one — bounded by the same constant the search's own term is bounded by, read from it
  rather than restated.
- **No migration, and no new index.** The `(Topic, TrainingId)` index ADR 0069 created serves an
  `IN` exactly as it served an equality, and the facets' join was already the shape this needs.
- **The spine's hue narrows its meaning.** A row wears the browsed topic's color only while exactly
  one shelf is lit; two shelves put the neutral tone back, for the reason ADR 0062 gave for a
  searched row — with several possible answers, a color would be a guess.

## Alternatives considered

**Intersection rather than union.** Defensible in the abstract — more filters, fewer results, the
behavior a table's column filters have. Here it is close to useless: a training sits on a handful of
the sixteen shelves, so a second ticked one would almost always answer an empty page, and the
interface would punish every second click. Put to the user explicitly and decided as *at least one*.

**Facet counts fixed to the whole catalog.** ADR 0069's position, and one request cheaper: the
chips could then be fetched once and never again. It leaves a visitor reading numbers that describe
a catalog they are no longer looking at, and a chip advertising twelve trainings that answers none
is worse than no chip.

**Counting the facets against the ticked shelves too.** Consistent-sounding, and wrong under a
widening filter: every lit shelf would report the size of the whole selection, and the row would
stop being a comparison. The counts answer the term because the term is the part of the question
the shelves do not widen.

**A joined parameter — `?topics=Design,Programming`.** One parameter, one value, no repetition. It
invents a separator the query-string convention already has, breaks every existing `?topic=` link
unless both spellings are kept, and puts a parser where model binding was doing the work.

## Verification

- **`TheCatalogsFacets_AreCountedUnderTheSameQuestionTheSearchAnswers` watched failing** three
  ways: with the visibility predicate copied into the facets' read, with the token loop copied
  beside it, and with the facets narrowed by nothing at all.
- **`KnownTopicAttributeTests` written red first**: a list carrying one name no topic answers to was
  accepted by the attribute before the sequence arm existed, which is the silent failure above.
- **SQLite facts** on the real schema: two shelves answer their union rather than their
  intersection, a training on both is answered once, no shelf answers the whole catalog, two shelves
  and a term narrow across both; a term's facets count only what it leaves standing, and a shelf the
  term empties is absent rather than zero.
- **End to end, on both hosts** (`CatalogSearchTest`, Docker-backed): a two-shelf search answers the
  union, and the facets of a term count that term's population.
- **The screen**, in bUnit: a second chip asks for both shelves, one lit chip lifts its own shelf
  alone, the address carries both sorted whatever the click order, and *Clear topics* empties the
  selection and is absent until there is one.
